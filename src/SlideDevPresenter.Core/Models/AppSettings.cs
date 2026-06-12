namespace SlideDevPresenter.Core.Models;

public sealed class AppSettings
{
    public List<PresentationSource> Sources { get; set; } = [];
    public DefaultSettings Defaults { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public WebViewSettings WebView { get; set; } = new();
    public DisplayManagementSettings DisplayManagement { get; set; } = new();
    public ShortcutsSettings Shortcuts { get; set; } = new();
    public NavigationSettings Navigation { get; set; } = new();
}

public sealed class DefaultSettings
{
    public string DefaultMode { get; set; } = "Presenter";
    public int DefaultPort { get; set; } = 3030;
    public bool AutoIncrementPorts { get; set; } = true;
    public bool OpenParticipantOnSecondMonitor { get; set; } = true;
    public bool FullscreenParticipant { get; set; } = true;
    public bool OpenPresenterOnStart { get; set; } = true;
}

public sealed class AppearanceSettings
{
    public string Theme { get; set; } = "System";
    public bool ShowStatusBar { get; set; } = true;
    public bool ShowRibbon { get; set; } = true;
}

public sealed class WebViewSettings
{
    public bool PreferEmbeddedWebView { get; set; } = true;
    public bool AllowExternalBrowserFallback { get; set; } = true;
}

public sealed class DisplayManagementSettings
{
    public bool AutoDetectDisplays { get; set; } = true;
    public bool FullscreenParticipantView { get; set; } = true;
    public bool RestoreDisplayTopologyOnExit { get; set; } = false;
}

/// <summary>
/// User-defined keyboard shortcut overrides for presentation actions.
/// A null value means "use the OS-specific default".
/// Values are stored as Avalonia KeyGesture strings (e.g. "F5", "Shift+F5").
/// </summary>
public sealed class ShortcutsSettings
{
    public string? StartFromBeginning { get; set; }
    public string? StartFromCurrentSlide { get; set; }
    public string? StartPresenterView { get; set; }
}

public sealed class NavigationSettings
{
    public bool OpenExternalLinksInSystemBrowser { get; set; } = true;
    public bool OpenExternalLinksInEmbeddedBrowser { get; set; } = true;
}
