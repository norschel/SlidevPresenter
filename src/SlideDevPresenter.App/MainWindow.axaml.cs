using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
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
        EmbeddedWebView.NavigationStarted += OnEmbeddedWebViewNavigationStarted;
        EmbeddedWebView.NewWindowRequested += OnEmbeddedWebViewNewWindowRequested;
        EmbeddedWebView.KeyDown += OnWebViewKeyDown;
    }

    private void ConfigureBrowserWebView()
    {
        BrowserWebView.NavigationStarted += OnBrowserWebViewNavigationStarted;
        BrowserWebView.NewWindowRequested += OnBrowserWebViewNewWindowRequested;
        BrowserWebView.KeyDown += OnWebViewKeyDown;
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
}
