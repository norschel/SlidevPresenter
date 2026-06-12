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

    [Fact]
    public async Task SaveAsync_PersistsCustomShortcuts()
    {
        var settings = new FakeSettingsService();
        var vm = new SettingsViewModel(settings, new FakeThemeService())
        {
            StartFromBeginningShortcut = "Ctrl+F5",
            StartFromCurrentSlideShortcut = "Ctrl+Shift+F5",
            StartPresenterViewShortcut = "Ctrl+Alt+F5"
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Ctrl+F5", settings.Settings.Shortcuts.StartFromBeginning);
        Assert.Equal("Ctrl+Shift+F5", settings.Settings.Shortcuts.StartFromCurrentSlide);
        Assert.Equal("Ctrl+Alt+F5", settings.Settings.Shortcuts.StartPresenterView);
    }

    [Fact]
    public async Task SaveAsync_EmptyShortcutSavedAsNull()
    {
        var settings = new FakeSettingsService();
        var vm = new SettingsViewModel(settings, new FakeThemeService())
        {
            StartFromBeginningShortcut = ""
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(settings.Settings.Shortcuts.StartFromBeginning);
    }

    [Fact]
    public void RestoreDefaultShortcutsCommand_ClearsAllShortcuts()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Shortcuts.StartFromBeginning = "Ctrl+F5";
        var vm = new SettingsViewModel(settings, new FakeThemeService());

        vm.RestoreDefaultShortcutsCommand.Execute(null);

        Assert.Equal(string.Empty, vm.StartFromBeginningShortcut);
        Assert.Equal(string.Empty, vm.StartFromCurrentSlideShortcut);
        Assert.Equal(string.Empty, vm.StartPresenterViewShortcut);
    }

    [Fact]
    public void ShortcutConflictError_WhenDuplicates_ReturnsErrorMessage()
    {
        var settings = new FakeSettingsService();
        var vm = new SettingsViewModel(settings, new FakeThemeService())
        {
            StartFromBeginningShortcut = "F5",
            StartFromCurrentSlideShortcut = "F5"
        };

        Assert.NotNull(vm.ShortcutConflictError);
        Assert.Contains("Duplicate", vm.ShortcutConflictError);
    }

    [Fact]
    public void ShortcutConflictError_WhenNoDuplicates_ReturnsNull()
    {
        var settings = new FakeSettingsService();
        var vm = new SettingsViewModel(settings, new FakeThemeService())
        {
            StartFromBeginningShortcut = "F5",
            StartFromCurrentSlideShortcut = "Shift+F5",
            StartPresenterViewShortcut = "Alt+F5"
        };

        Assert.Null(vm.ShortcutConflictError);
    }

    [Fact]
    public void LoadFromSettings_LoadsExistingShortcuts()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Shortcuts.StartFromBeginning = "Ctrl+F5";
        var vm = new SettingsViewModel(settings, new FakeThemeService());

        Assert.Equal("Ctrl+F5", vm.StartFromBeginningShortcut);
    }

    [Fact]
    public async Task SaveAsync_PersistsNavigationSettings()
    {
        var settings = new FakeSettingsService();
        var vm = new SettingsViewModel(settings, new FakeThemeService())
        {
            OpenExternalLinksInSystemBrowser = false,
            OpenExternalLinksInEmbeddedBrowser = true
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(settings.Settings.Navigation.OpenExternalLinksInSystemBrowser);
        Assert.True(settings.Settings.Navigation.OpenExternalLinksInEmbeddedBrowser);
    }
}
