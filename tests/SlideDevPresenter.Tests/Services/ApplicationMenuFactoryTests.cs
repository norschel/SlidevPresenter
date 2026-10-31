using Avalonia.Controls;
using Avalonia.Input;
using SlideDevPresenter.App.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class ApplicationMenuFactoryTests
{
    [Fact]
    public void MacApplicationMenu_InvokesSettingsAboutRestoreAndQuitActions()
    {
        var actions = new List<string>();
        var menu = ApplicationMenuFactory.Create(
            isMacOS: true,
            openSettings: () => actions.Add("settings"),
            openAbout: () => actions.Add("about"),
            restoreChrome: () => actions.Add("restore"),
            quit: () => actions.Add("quit"));

        var appMenu = Assert.Single(menu.Items.OfType<NativeMenuItem>());
        Assert.Equal("Slidev Presenter", appMenu.Header);
        var items = Assert.IsType<NativeMenu>(appMenu.Menu).Items.OfType<NativeMenuItem>()
            .Where(item => item.Header?.ToString() != "-")
            .ToDictionary(item => item.Header?.ToString() ?? string.Empty);

        items["Settings…"].Command!.Execute(null);
        items["About Slidev Presenter"].Command!.Execute(null);
        items["Restore Chrome"].Command!.Execute(null);
        items["Quit Slidev Presenter"].Command!.Execute(null);

        Assert.Equal(["settings", "about", "restore", "quit"], actions);
        Assert.Equal(new KeyGesture(Key.OemComma, KeyModifiers.Meta), items["Settings…"].Gesture);
        Assert.Equal(new KeyGesture(Key.Q, KeyModifiers.Meta), items["Quit Slidev Presenter"].Gesture);
    }

    [Fact]
    public void NonMacApplicationMenu_IsNotCreated()
    {
        var menu = ApplicationMenuFactory.Create(
            isMacOS: false,
            openSettings: () => { },
            openAbout: () => { },
            restoreChrome: () => { },
            quit: () => { });

        Assert.Empty(menu.Items);
    }
}
