using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using SlideDevPresenter.App.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class ApplicationMenuFactoryTests
{
    [Fact]
    public void MacApplicationMenu_HasTopLevelMenusInOrder()
    {
        var menu = ApplicationMenuFactory.Create(isMacOS: true, CreateCommands([]));

        var headers = menu.Items.OfType<NativeMenuItem>().Select(HeaderOf);

        Assert.Equal(["File", "Presentation", "View", "Window", "Help"], headers);
    }

    [Theory]
    [InlineData("File", "Refresh Library|Import Library…|Export Library…|-|Open Deck Location|Copy Deck Location|Open in VS Code|Export PDF…|-|Remove from Library")]
    [InlineData("Presentation", "Start from Beginning|Start from Current Slide|Start Presenter View|-|Resume|Stop Presentation|Retry|-|Swap Displays|Detect Displays")]
    [InlineData("View", "Home Tab|Library Tab|View Tab|Presentation Tab|Browser Tab|-|Presenter View|Participant View|-|Thumbnails Panel|Agenda Panel|Timer Panel|Restore Chrome|-|Open Current View in Browser|Open Presenter in Browser|Open Participant in Browser")]
    [InlineData("Window", "Minimize|Zoom")]
    [InlineData("Help", "Keyboard Shortcuts…|-|Slidev Documentation|Report an Issue")]
    public void MacApplicationMenu_ListsItemsInOrder(string menuHeader, string expectedItems)
    {
        var menu = ApplicationMenuFactory.Create(isMacOS: true, CreateCommands([]));

        var headers = GetSubmenu(menu, menuHeader).Items.OfType<NativeMenuItem>().Select(HeaderOf);

        Assert.Equal(expectedItems, string.Join('|', headers));
    }

    [Theory]
    [InlineData("File", "Refresh Library", nameof(ApplicationMenuCommands.RefreshLibrary))]
    [InlineData("File", "Import Library…", nameof(ApplicationMenuCommands.ImportLibrary))]
    [InlineData("File", "Export Library…", nameof(ApplicationMenuCommands.ExportLibrary))]
    [InlineData("File", "Open Deck Location", nameof(ApplicationMenuCommands.OpenDeckLocation))]
    [InlineData("File", "Copy Deck Location", nameof(ApplicationMenuCommands.CopyDeckLocation))]
    [InlineData("File", "Open in VS Code", nameof(ApplicationMenuCommands.OpenInVsCode))]
    [InlineData("File", "Export PDF…", nameof(ApplicationMenuCommands.ExportPdf))]
    [InlineData("File", "Remove from Library", nameof(ApplicationMenuCommands.RemoveFromLibrary))]
    [InlineData("Presentation", "Start from Beginning", nameof(ApplicationMenuCommands.StartFromBeginning))]
    [InlineData("Presentation", "Start from Current Slide", nameof(ApplicationMenuCommands.StartFromCurrentSlide))]
    [InlineData("Presentation", "Start Presenter View", nameof(ApplicationMenuCommands.StartPresenterView))]
    [InlineData("Presentation", "Resume", nameof(ApplicationMenuCommands.Resume))]
    [InlineData("Presentation", "Stop Presentation", nameof(ApplicationMenuCommands.Stop))]
    [InlineData("Presentation", "Retry", nameof(ApplicationMenuCommands.Retry))]
    [InlineData("Presentation", "Swap Displays", nameof(ApplicationMenuCommands.SwapDisplays))]
    [InlineData("Presentation", "Detect Displays", nameof(ApplicationMenuCommands.DetectDisplays))]
    [InlineData("View", "Home Tab", nameof(ApplicationMenuCommands.ShowHomeTab))]
    [InlineData("View", "Library Tab", nameof(ApplicationMenuCommands.ShowLibraryTab))]
    [InlineData("View", "View Tab", nameof(ApplicationMenuCommands.ShowViewTab))]
    [InlineData("View", "Presentation Tab", nameof(ApplicationMenuCommands.ShowPresentationTab))]
    [InlineData("View", "Browser Tab", nameof(ApplicationMenuCommands.ShowBrowserTab))]
    [InlineData("View", "Presenter View", nameof(ApplicationMenuCommands.UsePresenterSurface))]
    [InlineData("View", "Participant View", nameof(ApplicationMenuCommands.UseParticipantSurface))]
    [InlineData("View", "Thumbnails Panel", nameof(ApplicationMenuCommands.ToggleThumbnailsPanel))]
    [InlineData("View", "Agenda Panel", nameof(ApplicationMenuCommands.ToggleAgendaPanel))]
    [InlineData("View", "Timer Panel", nameof(ApplicationMenuCommands.ToggleTimerPanel))]
    [InlineData("View", "Restore Chrome", nameof(ApplicationMenuCommands.RestoreChrome))]
    [InlineData("View", "Open Current View in Browser", nameof(ApplicationMenuCommands.OpenCurrentViewInBrowser))]
    [InlineData("View", "Open Presenter in Browser", nameof(ApplicationMenuCommands.OpenPresenterInBrowser))]
    [InlineData("View", "Open Participant in Browser", nameof(ApplicationMenuCommands.OpenParticipantInBrowser))]
    [InlineData("Window", "Minimize", nameof(ApplicationMenuCommands.Minimize))]
    [InlineData("Window", "Zoom", nameof(ApplicationMenuCommands.Zoom))]
    [InlineData("Help", "Keyboard Shortcuts…", nameof(ApplicationMenuCommands.ShowKeyboardShortcuts))]
    [InlineData("Help", "Slidev Documentation", nameof(ApplicationMenuCommands.OpenDocumentation))]
    [InlineData("Help", "Report an Issue", nameof(ApplicationMenuCommands.ReportIssue))]
    public void MacApplicationMenu_ItemExecutesItsCommand(string menuHeader, string itemHeader, string expectedCommand)
    {
        var log = new List<string>();
        var menu = ApplicationMenuFactory.Create(isMacOS: true, CreateCommands(log));

        GetActionItems(menu, menuHeader)[itemHeader].Command!.Execute(null);

        Assert.Equal(expectedCommand, Assert.Single(log));
    }

    [Fact]
    public void MacApplicationMenu_EveryActionItemHasACommand()
    {
        var menu = ApplicationMenuFactory.Create(isMacOS: true, CreateCommands([]));

        Assert.All(GetAllActionItems(menu), item => Assert.NotNull(item.Command));
    }

    [Theory]
    [InlineData("File", "Refresh Library", Key.R)]
    [InlineData("View", "Home Tab", Key.D1)]
    [InlineData("View", "Library Tab", Key.D2)]
    [InlineData("View", "View Tab", Key.D3)]
    [InlineData("View", "Presentation Tab", Key.D4)]
    [InlineData("View", "Browser Tab", Key.D5)]
    [InlineData("Window", "Minimize", Key.M)]
    public void MacApplicationMenu_UsesCommandKeyShortcuts(string menuHeader, string itemHeader, Key key)
    {
        var menu = ApplicationMenuFactory.Create(isMacOS: true, CreateCommands([]));

        Assert.Equal(new KeyGesture(key, KeyModifiers.Meta), GetActionItems(menu, menuHeader)[itemHeader].Gesture);
    }

    [Fact]
    public void MacApplicationMenu_ShortcutsAreUnique()
    {
        var commands = CreateCommands(
            [],
            new KeyGesture(Key.Enter, KeyModifiers.Meta | KeyModifiers.Shift),
            new KeyGesture(Key.Enter, KeyModifiers.Meta),
            new KeyGesture(Key.Enter, KeyModifiers.Alt));
        var menu = ApplicationMenuFactory.Create(isMacOS: true, commands);
        var applicationMenu = CreateApplicationMenu([]);

        var gestures = GetAllActionItems(menu)
            .Concat(ActionItemsOf(applicationMenu).Values)
            .Where(item => item.Gesture is not null)
            .Select(item => item.Gesture!.ToString())
            .ToList();

        Assert.Equal(gestures.Count, gestures.Distinct().Count());
    }

    [Fact]
    public void MacApplicationMenus_LeaveCommandQToTheNativeQuitItem()
    {
        var menu = ApplicationMenuFactory.Create(isMacOS: true, CreateCommands([]));
        var applicationMenu = CreateApplicationMenu([]);

        var gestures = GetAllActionItems(menu)
            .Concat(ActionItemsOf(applicationMenu).Values)
            .Where(item => item.Gesture is not null)
            .Select(item => item.Gesture!.ToString());

        Assert.DoesNotContain(new KeyGesture(Key.Q, KeyModifiers.Meta).ToString(), gestures);
    }

    [Fact]
    public void PresentationMenu_UsesConfiguredStartGestures()
    {
        var fromBeginning = new KeyGesture(Key.Enter, KeyModifiers.Meta | KeyModifiers.Shift);
        var fromCurrentSlide = new KeyGesture(Key.Enter, KeyModifiers.Meta);
        var presenterView = new KeyGesture(Key.Enter, KeyModifiers.Alt);
        var menu = ApplicationMenuFactory.Create(
            isMacOS: true,
            CreateCommands([], fromBeginning, fromCurrentSlide, presenterView));

        var items = GetActionItems(menu, "Presentation");

        Assert.Equal(fromBeginning, items["Start from Beginning"].Gesture);
        Assert.Equal(fromCurrentSlide, items["Start from Current Slide"].Gesture);
        Assert.Equal(presenterView, items["Start Presenter View"].Gesture);
        Assert.Null(items["Resume"].Gesture);
    }

    [Fact]
    public void PresentationMenu_WithoutConfiguredGestures_HasNoStartShortcuts()
    {
        var menu = ApplicationMenuFactory.Create(isMacOS: true, CreateCommands([]));

        var items = GetActionItems(menu, "Presentation");

        Assert.Null(items["Start from Beginning"].Gesture);
        Assert.Null(items["Start from Current Slide"].Gesture);
        Assert.Null(items["Start Presenter View"].Gesture);
    }

    [Fact]
    public void UpdateStartGestures_ReplacesStartShortcutsOnTheExistingItems()
    {
        var menu = ApplicationMenuFactory.Create(
            isMacOS: true,
            CreateCommands([], new KeyGesture(Key.F5), new KeyGesture(Key.F5, KeyModifiers.Shift), new KeyGesture(Key.F5, KeyModifiers.Alt)));
        var original = GetActionItems(menu, "Presentation");
        var fromBeginning = new KeyGesture(Key.Enter, KeyModifiers.Meta | KeyModifiers.Shift);
        var fromCurrentSlide = new KeyGesture(Key.Enter, KeyModifiers.Meta);
        var presenterView = new KeyGesture(Key.Enter, KeyModifiers.Alt);

        ApplicationMenuFactory.UpdateStartGestures(menu, fromBeginning, fromCurrentSlide, presenterView);

        var updated = GetActionItems(menu, "Presentation");
        Assert.Same(original["Start from Beginning"], updated["Start from Beginning"]);
        Assert.Same(original["Start from Current Slide"], updated["Start from Current Slide"]);
        Assert.Same(original["Start Presenter View"], updated["Start Presenter View"]);
        Assert.Equal(fromBeginning, updated["Start from Beginning"].Gesture);
        Assert.Equal(fromCurrentSlide, updated["Start from Current Slide"].Gesture);
        Assert.Equal(presenterView, updated["Start Presenter View"].Gesture);
        Assert.Null(updated["Resume"].Gesture);
        Assert.Equal(new KeyGesture(Key.R, KeyModifiers.Meta), GetActionItems(menu, "File")["Refresh Library"].Gesture);
    }

    [Fact]
    public void UpdateStartGestures_WithNullGestures_ClearsStartShortcuts()
    {
        var menu = ApplicationMenuFactory.Create(
            isMacOS: true,
            CreateCommands([], new KeyGesture(Key.F5), new KeyGesture(Key.F5, KeyModifiers.Shift), new KeyGesture(Key.F5, KeyModifiers.Alt)));

        ApplicationMenuFactory.UpdateStartGestures(menu, null, null, null);

        var items = GetActionItems(menu, "Presentation");
        Assert.Null(items["Start from Beginning"].Gesture);
        Assert.Null(items["Start from Current Slide"].Gesture);
        Assert.Null(items["Start Presenter View"].Gesture);
    }

    [Fact]
    public void UpdateStartGestures_WithoutPresentationMenu_LeavesMenuEmpty()
    {
        var menu = ApplicationMenuFactory.Create(isMacOS: false, CreateCommands([]));

        ApplicationMenuFactory.UpdateStartGestures(menu, new KeyGesture(Key.F5), null, null);

        Assert.Empty(menu.Items);
    }

    [Fact]
    public void UpdateStartGestures_WithoutMenu_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ApplicationMenuFactory.UpdateStartGestures(null!, null, null, null));
    }

    [Fact]
    public void MacApplicationMenu_DisablesItemsWhoseCommandCannotExecute()
    {
        var menu = ApplicationMenuFactory.Create(
            isMacOS: true,
            CreateCommands([], exportPdf: new RelayCommand(() => { }, () => false)));

        var items = GetActionItems(menu, "File");

        Assert.False(items["Export PDF…"].IsEnabled);
        Assert.True(items["Import Library…"].IsEnabled);
    }

    [Fact]
    public void NonMacApplicationMenu_IsNotCreated()
    {
        var menu = ApplicationMenuFactory.Create(isMacOS: false, CreateCommands([]));

        Assert.Empty(menu.Items);
    }

    [Fact]
    public void Create_WithoutCommands_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ApplicationMenuFactory.Create(isMacOS: true, null!));
    }

    [Fact]
    public void ApplicationMenu_ListsAboutAndSettings()
    {
        var menu = CreateApplicationMenu([]);

        var headers = menu.Items.OfType<NativeMenuItem>().Select(HeaderOf);

        Assert.Equal("About Slidev Presenter|-|Settings…", string.Join('|', headers));
    }

    [Theory]
    [InlineData("About Slidev Presenter", "About")]
    [InlineData("Settings…", "Settings")]
    public void ApplicationMenu_ItemExecutesItsCommand(string itemHeader, string expectedCommand)
    {
        var log = new List<string>();
        var menu = CreateApplicationMenu(log);

        ActionItemsOf(menu)[itemHeader].Command!.Execute(null);

        Assert.Equal(expectedCommand, Assert.Single(log));
    }

    [Fact]
    public void ApplicationMenu_SettingsUsesCommandComma()
    {
        var menu = CreateApplicationMenu([]);

        Assert.Equal(new KeyGesture(Key.OemComma, KeyModifiers.Meta), ActionItemsOf(menu)["Settings…"].Gesture);
    }

    [Fact]
    public void ApplicationMenu_OnOtherPlatforms_IsEmpty()
    {
        var menu = CreateApplicationMenu([], isMacOS: false);

        Assert.Empty(menu.Items);
    }

    [Fact]
    public void ApplicationMenu_WithoutAboutCommand_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            ApplicationMenuFactory.CreateApplicationMenu(isMacOS: true, null!, new RelayCommand(() => { })));

        Assert.Equal("about", exception.ParamName);
    }

    [Fact]
    public void ApplicationMenu_WithoutSettingsCommand_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            ApplicationMenuFactory.CreateApplicationMenu(isMacOS: true, new RelayCommand(() => { }), null!));

        Assert.Equal("settings", exception.ParamName);
    }

    private static string HeaderOf(NativeMenuItem item) => item.Header?.ToString() ?? string.Empty;

    private static NativeMenu GetSubmenu(NativeMenu menu, string header)
    {
        var item = menu.Items.OfType<NativeMenuItem>().Single(candidate => HeaderOf(candidate) == header);
        return Assert.IsType<NativeMenu>(item.Menu);
    }

    private static Dictionary<string, NativeMenuItem> GetActionItems(NativeMenu menu, string header) =>
        ActionItemsOf(GetSubmenu(menu, header));

    private static Dictionary<string, NativeMenuItem> ActionItemsOf(NativeMenu menu) =>
        menu.Items.OfType<NativeMenuItem>()
            .Where(item => HeaderOf(item) != "-")
            .ToDictionary(HeaderOf);

    private static NativeMenu CreateApplicationMenu(List<string> log, bool isMacOS = true) =>
        ApplicationMenuFactory.CreateApplicationMenu(
            isMacOS,
            new RelayCommand(() => log.Add("About")),
            new RelayCommand(() => log.Add("Settings")));

    private static List<NativeMenuItem> GetAllActionItems(NativeMenu menu) =>
        menu.Items.OfType<NativeMenuItem>()
            .SelectMany(topLevel => GetActionItems(menu, HeaderOf(topLevel)).Values)
            .ToList();

    private static ApplicationMenuCommands CreateCommands(
        List<string> log,
        KeyGesture? startFromBeginning = null,
        KeyGesture? startFromCurrentSlide = null,
        KeyGesture? startPresenterView = null,
        ICommand? exportPdf = null)
    {
        RelayCommand Cmd(string name) => new(() => log.Add(name));

        return new ApplicationMenuCommands
        {
            RefreshLibrary = Cmd(nameof(ApplicationMenuCommands.RefreshLibrary)),
            ImportLibrary = Cmd(nameof(ApplicationMenuCommands.ImportLibrary)),
            ExportLibrary = Cmd(nameof(ApplicationMenuCommands.ExportLibrary)),
            OpenDeckLocation = Cmd(nameof(ApplicationMenuCommands.OpenDeckLocation)),
            CopyDeckLocation = Cmd(nameof(ApplicationMenuCommands.CopyDeckLocation)),
            OpenInVsCode = Cmd(nameof(ApplicationMenuCommands.OpenInVsCode)),
            ExportPdf = exportPdf ?? Cmd(nameof(ApplicationMenuCommands.ExportPdf)),
            RemoveFromLibrary = Cmd(nameof(ApplicationMenuCommands.RemoveFromLibrary)),
            StartFromBeginning = Cmd(nameof(ApplicationMenuCommands.StartFromBeginning)),
            StartFromCurrentSlide = Cmd(nameof(ApplicationMenuCommands.StartFromCurrentSlide)),
            StartPresenterView = Cmd(nameof(ApplicationMenuCommands.StartPresenterView)),
            StartFromBeginningGesture = startFromBeginning,
            StartFromCurrentSlideGesture = startFromCurrentSlide,
            StartPresenterViewGesture = startPresenterView,
            Resume = Cmd(nameof(ApplicationMenuCommands.Resume)),
            Stop = Cmd(nameof(ApplicationMenuCommands.Stop)),
            Retry = Cmd(nameof(ApplicationMenuCommands.Retry)),
            SwapDisplays = Cmd(nameof(ApplicationMenuCommands.SwapDisplays)),
            DetectDisplays = Cmd(nameof(ApplicationMenuCommands.DetectDisplays)),
            ShowHomeTab = Cmd(nameof(ApplicationMenuCommands.ShowHomeTab)),
            ShowLibraryTab = Cmd(nameof(ApplicationMenuCommands.ShowLibraryTab)),
            ShowViewTab = Cmd(nameof(ApplicationMenuCommands.ShowViewTab)),
            ShowPresentationTab = Cmd(nameof(ApplicationMenuCommands.ShowPresentationTab)),
            ShowBrowserTab = Cmd(nameof(ApplicationMenuCommands.ShowBrowserTab)),
            UsePresenterSurface = Cmd(nameof(ApplicationMenuCommands.UsePresenterSurface)),
            UseParticipantSurface = Cmd(nameof(ApplicationMenuCommands.UseParticipantSurface)),
            ToggleThumbnailsPanel = Cmd(nameof(ApplicationMenuCommands.ToggleThumbnailsPanel)),
            ToggleAgendaPanel = Cmd(nameof(ApplicationMenuCommands.ToggleAgendaPanel)),
            ToggleTimerPanel = Cmd(nameof(ApplicationMenuCommands.ToggleTimerPanel)),
            RestoreChrome = Cmd(nameof(ApplicationMenuCommands.RestoreChrome)),
            OpenCurrentViewInBrowser = Cmd(nameof(ApplicationMenuCommands.OpenCurrentViewInBrowser)),
            OpenPresenterInBrowser = Cmd(nameof(ApplicationMenuCommands.OpenPresenterInBrowser)),
            OpenParticipantInBrowser = Cmd(nameof(ApplicationMenuCommands.OpenParticipantInBrowser)),
            Minimize = Cmd(nameof(ApplicationMenuCommands.Minimize)),
            Zoom = Cmd(nameof(ApplicationMenuCommands.Zoom)),
            ShowKeyboardShortcuts = Cmd(nameof(ApplicationMenuCommands.ShowKeyboardShortcuts)),
            OpenDocumentation = Cmd(nameof(ApplicationMenuCommands.OpenDocumentation)),
            ReportIssue = Cmd(nameof(ApplicationMenuCommands.ReportIssue)),
        };
    }
}
