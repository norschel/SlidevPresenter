using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
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
    private readonly PresentationWindowService? _presentationWindowService;
    private readonly List<KeyBinding> _presentationKeyBindings = [];

    // Native WebView controls render in their own OS airspace and ignore a collapsed Avalonia
    // parent on macOS, so toggling IsVisible leaves the native surface painting over the other
    // workspaces. To reliably hide them we detach the control from its host panel entirely (which
    // removes the native view from the airspace) and re-attach it when it should be shown.
    private Panel? _embeddedWebViewHost;
    private Panel? _browserWebViewHost;
    private bool _embeddedWebViewsDisabledForSession;
    private bool _isPresentationBlanked;

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
        ConfigureNativeApplicationMenu();
        AttachEscapeHandler();
    }

    public MainWindow(
        MainViewModel viewModel,
        IShortcutService? shortcutService = null,
        IThemeService? themeService = null,
        IExternalBrowserLauncher? browserLauncher = null,
        WebViewNavigationPolicy? navigationPolicy = null,
        PresentationWindowService? presentationWindowService = null)
    {
        _viewModel = viewModel;
        _presentationWindowService = presentationWindowService;
        viewModel.ConfirmRepositoryUpdateAsync = ConfirmRepositoryUpdateAsync;
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
        ConfigureNativeApplicationMenu();
        AttachEscapeHandler();
    }

    private async void Settings_Click(object? sender, RoutedEventArgs e)
        => await OpenSettingsAsync();

    private async Task OpenSettingsAsync()
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

    private async void About_Click(object? sender, RoutedEventArgs e)
        => await OpenAboutAsync();

    private async Task OpenAboutAsync()
    {
        await new AboutWindow(_viewModel?.AppVersion ?? "unknown").ShowDialog(this);
    }

    private void ConfigureNativeApplicationMenu()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var menu = ApplicationMenuFactory.Create(
            isMacOS: true,
            openSettings: () => _ = OpenSettingsAsync(),
            openAbout: () => _ = OpenAboutAsync(),
            restoreChrome: () =>
            {
                if (_viewModel?.RestoreChromeCommand.CanExecute(null) == true)
                    _ = _viewModel.RestoreChromeCommand.ExecuteAsync(null);
            },
            quit: QuitApplication);
        NativeMenu.SetMenu(this, menu);
    }

    private static void QuitApplication()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void BrowserTab_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: BrowserTabViewModel tab })
            _viewModel?.SelectBrowserTab(tab);
    }

    private void CloseBrowserTab_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: BrowserTabViewModel tab })
            _viewModel?.CloseBrowserTab(tab);
    }

    private async void Branch_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null || e.AddedItems.Count == 0
            || e.AddedItems[0] is not string branch
            || string.Equals(branch, _viewModel.SelectedBranch, StringComparison.Ordinal))
            return;

        await _viewModel.ChangeSelectedBranchAsync(branch);
    }

    private async Task<bool> ConfirmRepositoryUpdateAsync(string message)
    {
        var dialog = new Window
        {
            Title = "Repository changes",
            Width = 520,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var keep = new Button { Content = "Keep local copy" };
        var update = new Button { Content = "Continue in terminal" };
        keep.Click += (_, _) => dialog.Close(false);
        update.Click += (_, _) => dialog.Close(true);
        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Children = { keep, update }
                }
            }
        };
        var hadEmbeddedView = _embeddedWebViewHost?.Children.Contains(EmbeddedWebView) == true;
        var hadBrowserView = _browserWebViewHost?.Children.Contains(BrowserWebView) == true;
        if (hadEmbeddedView)
            _embeddedWebViewHost!.Children.Remove(EmbeddedWebView);
        if (hadBrowserView)
            _browserWebViewHost!.Children.Remove(BrowserWebView);
        try { return await dialog.ShowDialog<bool>(this); }
        finally
        {
            if (hadEmbeddedView)
                _embeddedWebViewHost!.Children.Add(EmbeddedWebView);
            if (hadBrowserView)
                _browserWebViewHost!.Children.Add(BrowserWebView);
        }
    }

    private async void ExportPdf_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || !_viewModel.CanExportPdf)
            return;

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export presentation to PDF",
                SuggestedFileName = "presentation.pdf",
                DefaultExtension = "pdf",
                FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }]
            });
            if (file is not null)
                await _viewModel.ExportPdfAsync(file.Path.LocalPath);
        }
        catch (Exception exception)
        {
            _viewModel.PdfExportMessage = $"PDF export failed: {exception.Message}";
        }
    }

    private async void ExportLibrary_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export library",
                SuggestedFileName = "SlideDevPresenter-library.json",
                DefaultExtension = "json",
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });
            if (file is null)
                return;

            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek)
            {
                stream.SetLength(0);
                stream.Position = 0;
            }
            await _viewModel.ExportLibraryAsync(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _viewModel.LibraryMessage = $"Library export failed: {exception.Message}";
        }
    }

    private async void ImportLibrary_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import library",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });
            if (files.Count == 0)
                return;

            await using var stream = await files[0].OpenReadAsync();
            await _viewModel.ImportLibraryAsync(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            _viewModel.LibraryMessage = $"Library import failed: {exception.Message}";
        }
    }

    private async void CopyProjectLocation_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedProject is null || Clipboard is null)
            return;

        await Clipboard.SetTextAsync(_viewModel.SelectedProject.Location);
        _viewModel.LibraryMessage = "Location copied to clipboard.";
    }

    private void ConfigureEmbeddedWebView()
    {
        _embeddedWebViewHost = EmbeddedWebView.Parent as Panel;

        // Keep native WebView detached by default so startup does not initialize WebView2
        // before it is actually needed.
        DetachWebViewIfAttached(_embeddedWebViewHost, EmbeddedWebView);

        EmbeddedWebView.NavigationStarted += OnEmbeddedWebViewNavigationStarted;
        EmbeddedWebView.NavigationCompleted += OnWebViewNavigationCompleted;
        EmbeddedWebView.NewWindowRequested += OnEmbeddedWebViewNewWindowRequested;
        EmbeddedWebView.WebMessageReceived += OnWebViewWebMessageReceived;
        EmbeddedWebView.KeyDown += OnWebViewKeyDown;
        SyncEmbeddedWebViewSource();
        SyncEmbeddedWebViewAttachment();
    }

    private void SyncEmbeddedWebViewSource()
    {
        // WebView2 on Windows fails when navigated to about:blank while being torn down after ESC;
        // the view is detached once the presentation stops, so keep the last real page instead.
        if (_embeddedWebViewsDisabledForSession
            || _viewModel?.CurrentSurfaceUriOrBlank is not { } uri || !uri.IsAbsoluteUri
            || string.Equals(uri.Scheme, "about", StringComparison.OrdinalIgnoreCase)
            || EmbeddedWebView.Source == uri)
            return;

        EmbeddedWebView.Source = uri;
    }

    private void ConfigureBrowserWebView()
    {
        _browserWebViewHost = BrowserWebView.Parent as Panel;

        // Keep native WebView detached by default so startup does not initialize WebView2
        // before it is actually needed.
        DetachWebViewIfAttached(_browserWebViewHost, BrowserWebView);

        BrowserWebView.NavigationStarted += OnBrowserWebViewNavigationStarted;
        BrowserWebView.NavigationCompleted += OnWebViewNavigationCompleted;
        BrowserWebView.NewWindowRequested += OnBrowserWebViewNewWindowRequested;
        BrowserWebView.WebMessageReceived += OnWebViewWebMessageReceived;
        BrowserWebView.KeyDown += OnWebViewKeyDown;
        SyncBrowserWebViewAttachment();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.CurrentSurfaceUriOrBlank):
                SyncEmbeddedWebViewSource();
                break;
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
        EmbeddedWebView.NavigationCompleted -= OnWebViewNavigationCompleted;
        EmbeddedWebView.NewWindowRequested -= OnEmbeddedWebViewNewWindowRequested;
        EmbeddedWebView.WebMessageReceived -= OnWebViewWebMessageReceived;
        EmbeddedWebView.KeyDown -= OnWebViewKeyDown;

        BrowserWebView.NavigationStarted -= OnBrowserWebViewNavigationStarted;
        BrowserWebView.NavigationCompleted -= OnWebViewNavigationCompleted;
        BrowserWebView.NewWindowRequested -= OnBrowserWebViewNewWindowRequested;
        BrowserWebView.WebMessageReceived -= OnWebViewWebMessageReceived;
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
        var fromBeginning = $"Start presentation ({_shortcutService.GetDisplayText(PresentationShortcutAction.StartFromBeginning)})";
        var fromCurrent = $"Start from selected slide ({_shortcutService.GetDisplayText(PresentationShortcutAction.StartFromCurrentSlide)})";
        var presenterView = $"Launch presenter view ({_shortcutService.GetDisplayText(PresentationShortcutAction.StartPresenterView)})";

        foreach (var button in new Control[] { StartFromBeginningButton, HomeStartFromBeginningButton, IdleStartFromBeginningButton })
            ToolTip.SetTip(button, fromBeginning);
        foreach (var button in new Control[] { StartFromCurrentButton, HomeStartFromCurrentButton, IdleStartFromCurrentButton })
            ToolTip.SetTip(button, fromCurrent);
        foreach (var button in new Control[] { StartPresenterViewButton, HomeStartPresenterViewButton, IdleStartPresenterViewButton })
            ToolTip.SetTip(button, presenterView);
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
        if (TryForwardKeyToParticipant(e) || TryHandlePresentationKey(e))
            return;

        TryHandleEscape(e);
    }

    // Keys a native WebView already handled never reach here; Slidev syncs navigation to the presenter view.
    private bool TryForwardKeyToParticipant(KeyEventArgs e)
    {
        if ((e.Handled && e.Key != Key.Space) || _presentationWindowService?.IsParticipantWindowOpen != true
            || e.Source is TextBox or NativeWebView
            || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
            return false;

        Key? key = e.Key switch
        {
            Key.B or Key.W or Key.N or Key.P or Key.OemPeriod or Key.Decimal => e.Key,
            Key.Right or Key.Down or Key.PageDown or Key.Enter => Key.N,
            Key.Left or Key.Up or Key.PageUp or Key.Back => Key.P,
            Key.Space => e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? Key.P : Key.N,
            _ => null
        };
        if (key is null)
            return false;

        e.Handled = true;
        _presentationWindowService.DispatchPresentationKey(key.Value);
        return true;
    }

    private void OnWebViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (TryHandlePresentationKey(e))
            return;

        // Fallback: catch ESC forwarded by NativeWebView managed KeyDown event.
        TryHandleEscape(e);
    }

    private async void OnWebViewNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || sender is not NativeWebView webView)
            return;

        try
        {
            if (ReferenceEquals(webView, EmbeddedWebView))
                _isPresentationBlanked = false;

            await webView.InvokeScript(ReferenceEquals(webView, BrowserWebView)
                ? WebViewKeyboardBridge.EscapeScript + WebViewKeyboardBridge.DiagnosticsScript
                : WebViewKeyboardBridge.Script + WebViewKeyboardBridge.DiagnosticsScript);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError($"Could not install WebView keyboard bridge: {exception}");
        }
    }

    private void OnWebViewWebMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        if (WebViewKeyboardBridge.GetActionableDiagnostic(e.Body) is { } diagnostic)
        {
            _viewModel?.ReportWebViewDiagnostic(diagnostic);
            System.Diagnostics.Trace.TraceWarning(diagnostic);
        }

        if (ReferenceEquals(sender, EmbeddedWebView) && WebViewKeyboardBridge.GetBlankState(e.Body) is bool isBlanked)
        {
            _isPresentationBlanked = isBlanked;
            // The blank overlay is injected per page and not synced by Slidev, so mirror it to the participant view.
            _presentationWindowService?.DispatchPresentationKey(!isBlanked ? Key.Escape
                : WebViewKeyboardBridge.IsWhiteBlankMessage(e.Body) ? Key.W : Key.B);
        }

        if (WebViewKeyboardBridge.IsEscapeMessage(e.Body))
            HandleEscape();
    }

    private bool TryHandlePresentationKey(KeyEventArgs e) =>
        e.Handled || (!_embeddedWebViewsDisabledForSession && _viewModel?.CanShowEmbeddedSurface == true
            && WebViewKeyboardBridge.TryHandleKey(EmbeddedWebView, e, _isPresentationBlanked));

    private void TryHandleEscape(KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Escape)
            return;

        if (HandleEscape())
            e.Handled = true;
    }

    private bool HandleEscape()
    {
        if (_viewModel?.StopCommand.CanExecute(null) == true)
        {
            _viewModel.StopCommand.Execute(null);
            return true;
        }

        if (_viewModel?.IsBrowserRibbonSelected == true)
        {
            // When no presentation is running but the user is trapped on the Browser tab
            // (e.g. because they closed the participant window via the X button), ESC
            // switches back to the Home ribbon so the NativeWebView is unloaded.
            _viewModel.SelectHomeRibbon();
            return true;
        }

        return false;
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
