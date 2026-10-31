using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;

namespace SlideDevPresenter.App.Services;

/// <summary>Builds the native macOS application menu and keeps its actions testable.</summary>
public static class ApplicationMenuFactory
{
    /// <summary>Creates the application menu, leaving the native menu empty on non-macOS platforms.</summary>
    public static NativeMenu Create(
        bool isMacOS,
        Action openSettings,
        Action openAbout,
        Action restoreChrome,
        Action quit)
    {
        ArgumentNullException.ThrowIfNull(openSettings);
        ArgumentNullException.ThrowIfNull(openAbout);
        ArgumentNullException.ThrowIfNull(restoreChrome);
        ArgumentNullException.ThrowIfNull(quit);

        var menu = new NativeMenu();
        if (!isMacOS)
            return menu;

        var appMenu = new NativeMenuItem("Slidev Presenter")
        {
            Menu = new NativeMenu
            {
                Items =
                {
                    new NativeMenuItem("About Slidev Presenter")
                    {
                        Command = new RelayCommand(openAbout)
                    },
                    new NativeMenuItemSeparator(),
                    new NativeMenuItem("Settings…")
                    {
                        Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Meta),
                        Command = new RelayCommand(openSettings)
                    },
                    new NativeMenuItem("Restore Chrome")
                    {
                        Command = new RelayCommand(restoreChrome)
                    },
                    new NativeMenuItemSeparator(),
                    new NativeMenuItem("Quit Slidev Presenter")
                    {
                        Gesture = new KeyGesture(Key.Q, KeyModifiers.Meta),
                        Command = new RelayCommand(quit)
                    }
                }
            }
        };
        menu.Items.Add(appMenu);
        return menu;
    }
}
