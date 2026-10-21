using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class SettingsServiceTests
{
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
