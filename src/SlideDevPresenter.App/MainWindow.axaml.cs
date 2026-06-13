using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Threading;
using SlideDevPresenter.App.Services;
using SlideDevPresenter.App.ViewModels;
using SlideDevPresenter.App.Views;

namespace SlideDevPresenter.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel? _viewModel;
    private readonly IShortcutService _shortcutService;
    private readonly IThemeService _themeService;
    private readonly IExternalBrowserLauncher _browserLauncher;
    private readonly WebViewNavigationPolicy _navigationPolicy;
    private readonly List<KeyBinding> _presentationKeyBindings = [];

    // Native WebView controls render in their own OS airspace and ignore a collapsed Avalonia
    // parent on macOS, so toggling IsVisible leaves the native surface painting over the other
    // workspaces. To reliably hide them we detach the control from its host panel entirely (which
    // removes the native view from the airspace) and re-attach it when it should be shown.
    private Panel? _embeddedWebViewHost;
    private Panel? _browserWebViewHost;
    private bool _embeddedWebViewsDisabledForSession;

    /// <summary>Parameterless constructor for Avalonia designer.</summary>
    public MainWindow()
    {
        _shortcutService = new ShortcutService(new RuntimePlatformInfo());
        _themeService = new ThemeService();
        _browserLauncher = new ExternalBrowserLauncher();
        _navigationPolicy = new WebViewNavigationPolicy();
        InitializeComponent();
        ConfigureEmbeddedWebView();
        ConfigureBrowserWebView();
        ConfigureShortcutBindings();
        ConfigureShortcutTooltips();
        AttachEscapeHandler();
    }

    public MainWindow(
        MainViewModel viewModel,
        IShortcutService? shortcutService = null,
        IThemeService? themeService = null,
        IExternalBrowserLauncher? browserLauncher = null,
        WebViewNavigationPolicy? navigationPolicy = null)
    {
        _viewModel = viewModel;
        _shortcutService = shortcutService ?? new ShortcutService(new RuntimePlatformInfo());
        _themeService = themeService ?? new ThemeService();
        _browserLauncher = browserLauncher ?? new ExternalBrowserLauncher();
        _navigationPolicy = navigationPolicy ?? new WebViewNavigationPolicy();
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        InitializeComponent();
        ConfigureEmbeddedWebView();
        ConfigureBrowserWebView();
        ConfigureShortcutBindings();
        ConfigureShortcutTooltips();
        AttachEscapeHandler();
    }

    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        var vm = new SettingsViewModel(_viewModel.SettingsService, _themeService);
        var win = new SettingsWindow(vm);
        await win.ShowDialog(this);
        _viewModel.RefreshPreferences();
        ReconfigureShortcuts();
        await _viewModel.RefreshLibraryAsync();
    }

    private void ConfigureEmbeddedWebView()
    {
        _embeddedWebViewHost = EmbeddedWebView.Parent as Panel;

        // Keep native WebView detached by default so startup does not initialize WebView2
        // before it is actually needed.
        DetachWebViewIfAttached(_embeddedWebViewHost, EmbeddedWebView);

        EmbeddedWebView.NavigationStarted += OnEmbeddedWebViewNavigationStarted;
        EmbeddedWebView.NewWindowRequested += OnEmbeddedWebViewNewWindowRequested;
        EmbeddedWebView.KeyDown += OnWebViewKeyDown;
        SyncEmbeddedWebViewAttachment();
    }

    private void ConfigureBrowserWebView()
    {
        _browserWebViewHost = BrowserWebView.Parent as Panel;

        // Keep native WebView detached by default so startup does not initialize WebView2
        // before it is actually needed.
        DetachWebViewIfAttached(_browserWebViewHost, BrowserWebView);

        BrowserWebView.NavigationStarted += OnBrowserWebViewNavigationStarted;
        BrowserWebView.NewWindowRequested += OnBrowserWebViewNewWindowRequested;
        BrowserWebView.KeyDown += OnWebViewKeyDown;
        SyncBrowserWebViewAttachment();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.CanShowEmbeddedSurface):
                SyncEmbeddedWebViewAttachment();
                break;
            case nameof(MainViewModel.IsBrowserSurfaceVisible):
                SyncBrowserWebViewAttachment();
                break;
        }
    }

    private void SyncEmbeddedWebViewAttachment()
        => DeferWebViewAttachment(_embeddedWebViewHost, EmbeddedWebView, () => !_embeddedWebViewsDisabledForSession && (_viewModel?.CanShowEmbeddedSurface ?? false));

    private void SyncBrowserWebViewAttachment()
        => DeferWebViewAttachment(_browserWebViewHost, BrowserWebView, () => !_embeddedWebViewsDisabledForSession && (_viewModel?.IsBrowserSurfaceVisible ?? false));

    private static void DetachWebViewIfAttached(Panel? host, Control webView)
    {
        if (host?.Children.Contains(webView) == true)
            host.Children.Remove(webView);
    }

    private static void DeferWebViewAttachment(Panel? host, Control webView, Func<bool> shouldBeVisible)
    {
        if (host is null)
            return;

        // Native WebViews are mutated on macOS while the OS still owns the airspace. Detaching
        // synchronously during input/command handling (e.g. a fast ribbon-button click) can leave
        // the native surface stuck on top because no clean render cycle follows. Deferring to the
        // dispatcher lets the current input cycle finish first, so the detach is applied reliably
        // regardless of how briefly the button was pressed. The visibility is re-read inside the
        // closure so rapid successive switches always converge to the latest requested state.
        Dispatcher.UIThread.Post(() => ApplyWebViewAttachment(host, webView, shouldBeVisible()), DispatcherPriority.Background);
    }

    private static void ApplyWebViewAttachment(Panel host, Control webView, bool shouldBeVisible)
    {
        if (!shouldBeVisible && host.Children.Contains(webView))
        {
            // Detaching removes the native surface from the OS airspace on macOS.
            host.Children.Remove(webView);
            return;
        }

        var isAttached = host.Children.Contains(webView);

        if (shouldBeVisible && !isAttached)
        {
            // Restore the original XAML order: the WebView sits at index 0 (bottom of the
            // panel's z-order) with the placeholder content layered above it.
            host.Children.Insert(0, webView);
        }
    }

    public void DisableEmbeddedWebViewsForSession()
    {
        if (_embeddedWebViewsDisabledForSession)
            return;

        _embeddedWebViewsDisabledForSession = true;

        EmbeddedWebView.NavigationStarted -= OnEmbeddedWebViewNavigationStarted;
        EmbeddedWebView.NewWindowRequested -= OnEmbeddedWebViewNewWindowRequested;
        EmbeddedWebView.KeyDown -= OnWebViewKeyDown;

        BrowserWebView.NavigationStarted -= OnBrowserWebViewNavigationStarted;
        BrowserWebView.NewWindowRequested -= OnBrowserWebViewNewWindowRequested;
        BrowserWebView.KeyDown -= OnWebViewKeyDown;

        if (_embeddedWebViewHost?.Children.Contains(EmbeddedWebView) == true)
            _embeddedWebViewHost.Children.Remove(EmbeddedWebView);

        if (_browserWebViewHost?.Children.Contains(BrowserWebView) == true)
            _browserWebViewHost.Children.Remove(BrowserWebView);
    }

    private void OnEmbeddedWebViewNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        if (e.Request is null)
            return;

        if (!_navigationPolicy.ShouldOpenExternally(e.Request, EmbeddedWebView.Source))
            return;

        OpenExternalUrl(e.Request);
        e.Cancel = true;
    }

    private void OnEmbeddedWebViewNewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e)
    {
        if (e.Request is null)
            return;

        if (_navigationPolicy.ShouldOpenExternally(e.Request, EmbeddedWebView.Source))
        {
            OpenExternalUrl(e.Request);
            e.Handled = true;
            return;
        }

        EmbeddedWebView.Source = e.Request;
        e.Handled = true;
    }

    private void OnBrowserWebViewNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        if (e.Request is null || _viewModel is null)
            return;

        // Allow navigation within the browser workspace; update the selected tab's URL
        if (_navigationPolicy.ShouldOpenExternally(e.Request, BrowserWebView.Source))
        {
            OpenExternalUrl(e.Request);
            e.Cancel = true;
            return;
        }

        // Keep the selected tab's URL in sync when navigating within the browser workspace
        if (_viewModel.SelectedBrowserTab is { } tab && tab.Url != e.Request)
        {
            tab.Url = e.Request;
            tab.Title = e.Request.Host.Length > 0 ? e.Request.Host : e.Request.ToString();
        }
    }

    private void OnBrowserWebViewNewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e)
    {
        if (e.Request is null)
            return;

        if (_navigationPolicy.ShouldOpenExternally(e.Request, BrowserWebView.Source))
        {
            OpenExternalUrl(e.Request);
            e.Handled = true;
            return;
        }

        // Open as a new browser tab
        _viewModel?.OpenInEmbeddedBrowser(e.Request);
        e.Handled = true;
    }

    private void OpenExternalUrl(Uri uri)
    {
        var settings = _viewModel?.SettingsService.Settings.Navigation;

        if (settings is null || settings.OpenExternalLinksInSystemBrowser)
            _browserLauncher.Open(uri);

        if (settings is null || settings.OpenExternalLinksInEmbeddedBrowser)
            _viewModel?.OpenInEmbeddedBrowser(uri);
    }

    private void ConfigureShortcutBindings()
    {
        if (_viewModel is null)
            return;

        foreach (var binding in _presentationKeyBindings)
            KeyBindings.Remove(binding);
        _presentationKeyBindings.Clear();

        _presentationKeyBindings.Add(new KeyBinding
        {
            Gesture = _shortcutService.GetGesture(PresentationShortcutAction.StartFromBeginning),
            Command = _viewModel.StartFromBeginningCommand
        });
        _presentationKeyBindings.Add(new KeyBinding
        {
            Gesture = _shortcutService.GetGesture(PresentationShortcutAction.StartFromCurrentSlide),
            Command = _viewModel.StartFromCurrentSlideCommand
        });
        _presentationKeyBindings.Add(new KeyBinding
        {
            Gesture = _shortcutService.GetGesture(PresentationShortcutAction.StartPresenterView),
            Command = _viewModel.StartPresenterViewCommand
        });

        foreach (var binding in _presentationKeyBindings)
            KeyBindings.Add(binding);
    }

    private void ConfigureShortcutTooltips()
    {
        ToolTip.SetTip(StartFromBeginningButton, $"Start presentation ({_shortcutService.GetDisplayText(PresentationShortcutAction.StartFromBeginning)})");
        ToolTip.SetTip(StartFromCurrentButton, $"Start from selected slide ({_shortcutService.GetDisplayText(PresentationShortcutAction.StartFromCurrentSlide)})");
        ToolTip.SetTip(StartPresenterViewButton, $"Launch presenter view ({_shortcutService.GetDisplayText(PresentationShortcutAction.StartPresenterView)})");
    }

    /// <summary>Rebuilds keyboard shortcut bindings and tooltips from the current settings.</summary>
    public void ReconfigureShortcuts()
    {
        ConfigureShortcutBindings();
        ConfigureShortcutTooltips();
    }

    private void AttachEscapeHandler()
    {
        // BUG-006 (main window): Intercept ESC in the tunnel phase so it fires even when a
        // NativeWebView has native OS focus and the bubble-phase OnKeyDown override is bypassed.
        AddHandler(InputElement.KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        TryHandleEscape(e);
    }

    private void OnWebViewKeyDown(object? sender, KeyEventArgs e)
    {
        // Fallback: catch ESC forwarded by NativeWebView managed KeyDown event.
        TryHandleEscape(e);
    }

    private void TryHandleEscape(KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        if (_viewModel?.StopCommand.CanExecute(null) == true)
        {
            _viewModel.StopCommand.Execute(null);
            e.Handled = true;
        }
        else if (_viewModel?.IsBrowserRibbonSelected == true)
        {
            // When no presentation is running but the user is trapped on the Browser tab
            // (e.g. because they closed the participant window via the X button), ESC
            // switches back to the Presentation ribbon so the NativeWebView is unloaded.
            _viewModel.SelectPresentationRibbon();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Stop the active presentation when ESC is pressed in the main window.
        // This covers the common case where the user has focus on the main window
        // while the participant window is visible on the same or a different display.
        // Note: the tunnel handler (AttachEscapeHandler) is the primary path; this
        // override is kept as an additional fallback for the bubble phase.
        TryHandleEscape(e);
        base.OnKeyDown(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        base.OnClosed(e);
    }
}
