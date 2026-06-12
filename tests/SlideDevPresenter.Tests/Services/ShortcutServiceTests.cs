using Avalonia.Input;
using SlideDevPresenter.App.Services;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Tests.Services;

internal sealed class FakePlatformInfo(bool isMacOs) : IPlatformInfo
{
    public bool IsMacOS { get; } = isMacOs;
}

internal sealed class FakeSettingsServiceForShortcuts : ISettingsService
{
    public AppSettings Settings { get; } = new();
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class ShortcutServiceTests
{
    [Fact]
    public void MacKeymap_UsesExpectedGesturesAndDisplayText()
    {
        var service = new ShortcutService(new FakePlatformInfo(true));

        var fromBeginning = service.GetGesture(PresentationShortcutAction.StartFromBeginning);
        var fromCurrent = service.GetGesture(PresentationShortcutAction.StartFromCurrentSlide);
        var presenterView = service.GetGesture(PresentationShortcutAction.StartPresenterView);

        Assert.Equal(Key.Enter, fromBeginning.Key);
        Assert.Equal(KeyModifiers.Meta | KeyModifiers.Shift, fromBeginning.KeyModifiers);
        Assert.Equal(Key.Enter, fromCurrent.Key);
        Assert.Equal(KeyModifiers.Meta, fromCurrent.KeyModifiers);
        Assert.Equal(Key.Enter, presenterView.Key);
        Assert.Equal(KeyModifiers.Alt, presenterView.KeyModifiers);
        Assert.Equal("⌘+Shift+Return", service.GetDisplayText(PresentationShortcutAction.StartFromBeginning));
    }

    [Fact]
    public void WindowsAndLinuxKeymap_UsesExpectedGesturesAndDisplayText()
    {
        var service = new ShortcutService(new FakePlatformInfo(false));

        var fromBeginning = service.GetGesture(PresentationShortcutAction.StartFromBeginning);
        var fromCurrent = service.GetGesture(PresentationShortcutAction.StartFromCurrentSlide);
        var presenterView = service.GetGesture(PresentationShortcutAction.StartPresenterView);

        Assert.Equal(Key.F5, fromBeginning.Key);
        Assert.Equal(KeyModifiers.None, fromBeginning.KeyModifiers);
        Assert.Equal(Key.F5, fromCurrent.Key);
        Assert.Equal(KeyModifiers.Shift, fromCurrent.KeyModifiers);
        Assert.Equal(Key.F5, presenterView.Key);
        Assert.Equal(KeyModifiers.Alt, presenterView.KeyModifiers);
        Assert.Equal("F5", service.GetDisplayText(PresentationShortcutAction.StartFromBeginning));
    }

    [Fact]
    public void CustomShortcut_OverridesDefaultGesture()
    {
        var settingsService = new FakeSettingsServiceForShortcuts();
        settingsService.Settings.Shortcuts.StartFromBeginning = "Ctrl+F5";
        var service = new ShortcutService(new FakePlatformInfo(false), settingsService);

        var gesture = service.GetGesture(PresentationShortcutAction.StartFromBeginning);

        Assert.Equal(Key.F5, gesture.Key);
        Assert.Equal(KeyModifiers.Control, gesture.KeyModifiers);
    }

    [Fact]
    public void CustomShortcut_DisplayTextReflectsCustomValue()
    {
        var settingsService = new FakeSettingsServiceForShortcuts();
        settingsService.Settings.Shortcuts.StartFromBeginning = "Ctrl+F5";
        var service = new ShortcutService(new FakePlatformInfo(false), settingsService);

        var text = service.GetDisplayText(PresentationShortcutAction.StartFromBeginning);

        Assert.Equal("Ctrl+F5", text);
    }

    [Fact]
    public void NullCustomShortcut_FallsBackToOsDefault()
    {
        var settingsService = new FakeSettingsServiceForShortcuts();
        settingsService.Settings.Shortcuts.StartFromBeginning = null;
        var service = new ShortcutService(new FakePlatformInfo(false), settingsService);

        var gesture = service.GetGesture(PresentationShortcutAction.StartFromBeginning);

        Assert.Equal(Key.F5, gesture.Key);
        Assert.Equal(KeyModifiers.None, gesture.KeyModifiers);
    }

    [Fact]
    public void InvalidCustomShortcut_FallsBackToOsDefault()
    {
        var settingsService = new FakeSettingsServiceForShortcuts();
        settingsService.Settings.Shortcuts.StartFromBeginning = "NOT_A_VALID_GESTURE!!!";
        var service = new ShortcutService(new FakePlatformInfo(false), settingsService);

        var gesture = service.GetGesture(PresentationShortcutAction.StartFromBeginning);

        Assert.Equal(Key.F5, gesture.Key);
        Assert.Equal(KeyModifiers.None, gesture.KeyModifiers);
    }

    [Fact]
    public void OnlyStartFromBeginning_CustomShortcut_OtherActionsUseDefaults()
    {
        var settingsService = new FakeSettingsServiceForShortcuts();
        settingsService.Settings.Shortcuts.StartFromBeginning = "Ctrl+F5";
        var service = new ShortcutService(new FakePlatformInfo(false), settingsService);

        var fromCurrent = service.GetGesture(PresentationShortcutAction.StartFromCurrentSlide);
        var presenterView = service.GetGesture(PresentationShortcutAction.StartPresenterView);

        Assert.Equal(Key.F5, fromCurrent.Key);
        Assert.Equal(KeyModifiers.Shift, fromCurrent.KeyModifiers);
        Assert.Equal(Key.F5, presenterView.Key);
        Assert.Equal(KeyModifiers.Alt, presenterView.KeyModifiers);
    }
}
