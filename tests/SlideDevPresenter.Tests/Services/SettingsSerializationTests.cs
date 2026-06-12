using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.Tests.Services;

public class SettingsSerializationTests
{
    private static SettingsService CreateService(string path) =>
        new(NullLogger<SettingsService>.Instance, path);

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsDefaultSettings()
    {
        var path = Path.GetTempFileName();
        try
        {
            var service = CreateService(path);
            await service.SaveAsync();

            var loaded = CreateService(path);
            await loaded.LoadAsync();

            Assert.Equal(service.Settings.Defaults.DefaultPort, loaded.Settings.Defaults.DefaultPort);
            Assert.Equal(service.Settings.Defaults.AutoIncrementPorts, loaded.Settings.Defaults.AutoIncrementPorts);
            Assert.Equal(service.Settings.Appearance.Theme, loaded.Settings.Appearance.Theme);
            Assert.Equal(service.Settings.WebView.PreferEmbeddedWebView, loaded.Settings.WebView.PreferEmbeddedWebView);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsSources()
    {
        var path = Path.GetTempFileName();
        try
        {
            var sourceId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var service = CreateService(path);
            service.Settings.Sources.Add(new PresentationSource
            {
                Id = sourceId,
                Name = "Talks",
                Type = PresentationSourceType.LocalRoot,
                Location = "~/talks",
                IsEnabled = true
            });
            service.Settings.Sources.Add(new PresentationSource
            {
                Id = Guid.NewGuid(),
                Name = "DevOps Days",
                Type = PresentationSourceType.HostedUrl,
                Location = "https://slides.example.com/devops-days",
                IsEnabled = false
            });
            await service.SaveAsync();

            var loaded = CreateService(path);
            await loaded.LoadAsync();

            Assert.Equal(2, loaded.Settings.Sources.Count);

            var first = loaded.Settings.Sources[0];
            Assert.Equal(sourceId, first.Id);
            Assert.Equal("Talks", first.Name);
            Assert.Equal(PresentationSourceType.LocalRoot, first.Type);
            Assert.Equal("~/talks", first.Location);
            Assert.True(first.IsEnabled);

            var second = loaded.Settings.Sources[1];
            Assert.Equal(PresentationSourceType.HostedUrl, second.Type);
            Assert.False(second.IsEnabled);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenFileNotFound_UsesDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var service = CreateService(path);
        await service.LoadAsync();

        Assert.NotNull(service.Settings);
        Assert.Empty(service.Settings.Sources);
        Assert.Equal(3030, service.Settings.Defaults.DefaultPort);
    }

    [Fact]
    public async Task LoadAsync_WhenFileIsCorrupt_UsesDefaults()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "this is not valid json {{{{");
            var service = CreateService(path);
            await service.LoadAsync();

            Assert.NotNull(service.Settings);
            Assert.Empty(service.Settings.Sources);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SavedJson_ContainsCamelCaseKeys()
    {
        var path = Path.GetTempFileName();
        try
        {
            var service = CreateService(path);
            await service.SaveAsync();

            var json = await File.ReadAllTextAsync(path);
            Assert.Contains("\"sources\"", json);
            Assert.Contains("\"defaults\"", json);
            Assert.Contains("\"defaultPort\"", json);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
