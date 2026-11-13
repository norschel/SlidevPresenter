using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task SaveAsync_PreCanceled_PreservesExistingSettingsFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SlideDevPresenter-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        const string previous = "{\"previous\":true}";
        await File.WriteAllTextAsync(path, previous);

        try
        {
            var service = new SettingsService(NullLogger<SettingsService>.Instance, path);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SaveAsync(cancellation.Token));

            Assert.Equal(previous, await File.ReadAllTextAsync(path));
            Assert.Equal("settings.json", Assert.Single(Directory.GetFiles(directory)).Split(Path.DirectorySeparatorChar).Last());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_WriteFailure_PreservesExistingSettingsFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SlideDevPresenter-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        const string previous = "{\"previous\":true}";
        await File.WriteAllTextAsync(path, previous);

        try
        {
            var service = new SettingsService(
                NullLogger<SettingsService>.Instance,
                path,
                (_, _, _) => Task.FromException(new IOException("Simulated serialization failure.")));

            var error = await Assert.ThrowsAsync<IOException>(() => service.SaveAsync());

            Assert.Equal("Simulated serialization failure.", error.Message);
            Assert.Equal(previous, await File.ReadAllTextAsync(path));
            Assert.Equal("settings.json", Assert.Single(Directory.GetFiles(directory)).Split(Path.DirectorySeparatorChar).Last());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(PresentationSourceType.LocalRoot, false)]
    [InlineData(PresentationSourceType.LocalProject, false)]
    [InlineData(PresentationSourceType.HostedUrl, true)]
    [InlineData(PresentationSourceType.GitHub, true)]
    public async Task ExportAsync_WritesAccessPropertiesOnlyForRemoteSources(PresentationSourceType type, bool expected)
    {
        var service = new SettingsService(NullLogger<SettingsService>.Instance, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json"));
        service.Settings.Sources.Add(new PresentationSource { Id = Guid.NewGuid(), Name = "Deck", Type = type, Location = "~/slides" });
        await using var stream = new MemoryStream();

        await service.ExportAsync(stream);
        stream.Position = 0;
        using var document = await JsonDocument.ParseAsync(stream);
        var source = Assert.Single(document.RootElement.GetProperty("sources").EnumerateArray());

        Assert.Equal(expected, source.TryGetProperty("isPublic", out _));
        Assert.Equal(expected, source.TryGetProperty("requiresAuthentication", out _));
    }
}
