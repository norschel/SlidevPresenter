using SlideDevPresenter.App.Services;
using SlideDevPresenter.App.ViewModels;

namespace SlideDevPresenter.Tests.ViewModels;

internal sealed class FakeThemeService : IThemeService
{
    public string? LastAppliedTheme { get; private set; }
    public int ApplyCallCount { get; private set; }

    public void ApplyTheme(string? theme)
    {
        ApplyCallCount++;
        LastAppliedTheme = theme;
    }
}

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task SaveAsync_PersistsAndAppliesThemeImmediately()
    {
        var settings = new FakeSettingsService();
        var themeService = new FakeThemeService();
        var vm = new SettingsViewModel(settings, themeService)
        {
            Theme = "Dark"
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Dark", settings.Settings.Appearance.Theme);
        Assert.Equal(1, themeService.ApplyCallCount);
        Assert.Equal("Dark", themeService.LastAppliedTheme);
    }
}
