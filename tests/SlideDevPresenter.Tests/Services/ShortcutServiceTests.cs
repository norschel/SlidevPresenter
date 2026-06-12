using Avalonia.Input;
using SlideDevPresenter.App.Services;

namespace SlideDevPresenter.Tests.Services;

internal sealed class FakePlatformInfo(bool isMacOs) : IPlatformInfo
{
    public bool IsMacOS { get; } = isMacOs;
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
}
