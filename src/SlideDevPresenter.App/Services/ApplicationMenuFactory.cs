using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;

namespace SlideDevPresenter.App.Services;

/// <summary>Builds the native macOS menus and keeps their actions testable.</summary>
public static class ApplicationMenuFactory
{
    private const string PresentationMenuHeader = "Presentation";
    private const string StartFromBeginningHeader = "Start from Beginning";
    private const string StartFromCurrentSlideHeader = "Start from Current Slide";
    private const string StartPresenterViewHeader = "Start Presenter View";

    /// <summary>
    /// Creates the leading application menu (About, Settings…), leaving it empty on non-macOS platforms.
    /// Avalonia appends the standard Services, Hide, Hide Others, Show All and Quit items itself.
    /// </summary>
    public static NativeMenu CreateApplicationMenu(bool isMacOS, ICommand about, ICommand settings)
    {
        ArgumentNullException.ThrowIfNull(about);
        ArgumentNullException.ThrowIfNull(settings);

        var menu = new NativeMenu();
        if (!isMacOS)
            return menu;

        menu.Items.Add(Item("About Slidev Presenter", about));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item("Settings…", settings, new KeyGesture(Key.OemComma, KeyModifiers.Meta)));
        return menu;
    }

    /// <summary>Creates the window menus that follow the application menu, leaving the native menu empty on non-macOS platforms.</summary>
    public static NativeMenu Create(bool isMacOS, ApplicationMenuCommands commands)
    {
        ArgumentNullException.ThrowIfNull(commands);

        var menu = new NativeMenu();
        if (!isMacOS)
            return menu;

        menu.Items.Add(Submenu(
            "File",
            Item("Refresh Library", commands.RefreshLibrary, new KeyGesture(Key.R, KeyModifiers.Meta)),
            Item("Import Library…", commands.ImportLibrary),
            Item("Export Library…", commands.ExportLibrary),
            new NativeMenuItemSeparator(),
            Item("Open Deck Location", commands.OpenDeckLocation),
            Item("Copy Deck Location", commands.CopyDeckLocation),
            Item("Open in VS Code", commands.OpenInVsCode),
            Item("Export PDF…", commands.ExportPdf),
            new NativeMenuItemSeparator(),
            Item("Remove from Library", commands.RemoveFromLibrary)));

        menu.Items.Add(Submenu(
            PresentationMenuHeader,
            Item(StartFromBeginningHeader, commands.StartFromBeginning, commands.StartFromBeginningGesture),
            Item(StartFromCurrentSlideHeader, commands.StartFromCurrentSlide, commands.StartFromCurrentSlideGesture),
            Item(StartPresenterViewHeader, commands.StartPresenterView, commands.StartPresenterViewGesture),
            new NativeMenuItemSeparator(),
            Item("Resume", commands.Resume),
            Item("Stop Presentation", commands.Stop),
            Item("Retry", commands.Retry),
            new NativeMenuItemSeparator(),
            Item("Swap Displays", commands.SwapDisplays),
            Item("Detect Displays", commands.DetectDisplays)));

        menu.Items.Add(Submenu(
            "View",
            Item("Home Tab", commands.ShowHomeTab, new KeyGesture(Key.D1, KeyModifiers.Meta)),
            Item("Library Tab", commands.ShowLibraryTab, new KeyGesture(Key.D2, KeyModifiers.Meta)),
            Item("View Tab", commands.ShowViewTab, new KeyGesture(Key.D3, KeyModifiers.Meta)),
            Item("Presentation Tab", commands.ShowPresentationTab, new KeyGesture(Key.D4, KeyModifiers.Meta)),
            Item("Browser Tab", commands.ShowBrowserTab, new KeyGesture(Key.D5, KeyModifiers.Meta)),
            new NativeMenuItemSeparator(),
            Item("Presenter View", commands.UsePresenterSurface),
            Item("Participant View", commands.UseParticipantSurface),
            new NativeMenuItemSeparator(),
            Item("Thumbnails Panel", commands.ToggleThumbnailsPanel),
            Item("Agenda Panel", commands.ToggleAgendaPanel),
            Item("Timer Panel", commands.ToggleTimerPanel),
            Item("Restore Chrome", commands.RestoreChrome),
            new NativeMenuItemSeparator(),
            Item("Open Current View in Browser", commands.OpenCurrentViewInBrowser),
            Item("Open Presenter in Browser", commands.OpenPresenterInBrowser),
            Item("Open Participant in Browser", commands.OpenParticipantInBrowser)));

        menu.Items.Add(Submenu(
            "Window",
            Item("Minimize", commands.Minimize, new KeyGesture(Key.M, KeyModifiers.Meta)),
            Item("Zoom", commands.Zoom)));

        menu.Items.Add(Submenu(
            "Help",
            Item("Keyboard Shortcuts…", commands.ShowKeyboardShortcuts),
            new NativeMenuItemSeparator(),
            Item("Slidev Documentation", commands.OpenDocumentation),
            Item("Report an Issue", commands.ReportIssue)));

        return menu;
    }

    /// <summary>Applies the configured start shortcuts to the Presentation menu in place, so the native menu is not rebuilt.</summary>
    public static void UpdateStartGestures(
        NativeMenu menu,
        KeyGesture? startFromBeginning,
        KeyGesture? startFromCurrentSlide,
        KeyGesture? startPresenterView)
    {
        ArgumentNullException.ThrowIfNull(menu);

        var presentationMenu = menu.Items
            .OfType<NativeMenuItem>()
            .FirstOrDefault(item => item.Header is PresentationMenuHeader)
            ?.Menu;
        if (presentationMenu is null)
            return;

        foreach (var item in presentationMenu.Items.OfType<NativeMenuItem>())
        {
            switch (item.Header)
            {
                case StartFromBeginningHeader:
                    item.Gesture = startFromBeginning;
                    break;
                case StartFromCurrentSlideHeader:
                    item.Gesture = startFromCurrentSlide;
                    break;
                case StartPresenterViewHeader:
                    item.Gesture = startPresenterView;
                    break;
            }
        }
    }

    private static NativeMenuItem Item(string header, ICommand command, KeyGesture? gesture = null) =>
        new(header) { Command = command, Gesture = gesture };

    private static NativeMenuItem Submenu(string header, params NativeMenuItemBase[] items)
    {
        var submenu = new NativeMenu();
        foreach (var item in items)
            submenu.Items.Add(item);

        return new NativeMenuItem(header) { Menu = submenu };
    }
}
