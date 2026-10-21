using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SlideDevPresenter.App.Services;

namespace SlideDevPresenter.App.Views;

public partial class ParticipantWindow : Window
{
    private readonly string _participantUrl;
    private readonly IExternalBrowserLauncher _browserLauncher;
    private readonly WebViewNavigationPolicy _navigationPolicy;
    private readonly PresentationEscapeHandler _escapeHandler;
    private bool _exitRaised;
    private bool _isFullscreenRequested;
    private bool _isScreenBlanked;

    /// <summary>Raised when the user requests to exit the presentation (e.g. via ESC).</summary>
    public event EventHandler? PresentationExited;

    /// <summary>Raised when the user navigates to an external URL from within the presentation.</summary>
    public event EventHandler<Uri>? ExternalLinkNavigated;

    /// <summary>Parameterless constructor for the Avalonia designer.</summary>
    public ParticipantWindow()
    {
        _participantUrl = string.Empty;
        _browserLauncher = new ExternalBrowserLauncher();
        _navigationPolicy = new WebViewNavigationPolicy();
        _escapeHandler = new PresentationEscapeHandler();
        InitializeComponent();
        AttachHandlers();
    }

    public ParticipantWindow(
        string participantUrl,
        IExternalBrowserLauncher? browserLauncher = null,
        WebViewNavigationPolicy? navigationPolicy = null,
        PresentationEscapeHandler? escapeHandler = null)
    {
        _participantUrl = participantUrl;
        _browserLauncher = browserLauncher ?? new ExternalBrowserLauncher();
        _navigationPolicy = navigationPolicy ?? new WebViewNavigationPolicy();
        _escapeHandler = escapeHandler ?? new PresentationEscapeHandler();
        InitializeComponent();
        AttachHandlers();
        Opened += OnOpened;
    }

    /// <summary>Puts the window into fullscreen mode.</summary>
    public void SetFullscreen()
    {
        _isFullscreenRequested = true;
        WindowState = WindowState.FullScreen;
    }

    /// <summary>Moves the window to <paramref name="position"/> without treating the fullscreen exit as a user exit.</summary>
    public void MoveTo(PixelPoint position)
    {
        var wasFullscreen = _isFullscreenRequested;
        _isFullscreenRequested = false;

        if (WindowState != WindowState.Normal)
            WindowState = WindowState.Normal;

        Position = position;

        if (!wasFullscreen)
        {
            Dispatcher.UIThread.Post(FocusPresentation, DispatcherPriority.Background);
            return;
        }

        // Let the platform finish leaving fullscreen (async on macOS) before re-entering it on the new display.
        Dispatcher.UIThread.Post(() =>
        {
            Position = position;
            SetFullscreen();
            Dispatcher.UIThread.Post(FocusPresentation, DispatcherPriority.Background);
            // macOS ignores activation while the fullscreen animation runs; retry once it has settled.
            DispatcherTimer.RunOnce(FocusPresentation, TimeSpan.FromMilliseconds(900));
        }, DispatcherPriority.Background);
    }

    /// <summary>Sends a presentation key (blank/restore) to the participant slides.</summary>
    public void DispatchPresentationKey(Key key) =>
        _ = WebViewKeyboardBridge.DispatchKeyAsync(WebView, key, KeyModifiers.None, useFocusedElement: false);

    // Moving/re-fullscreening drops keyboard focus, so clicker and arrow keys would no longer reach the slides.
    private async void FocusPresentation()
    {
        Activate();
        WebView.Focus();

        // The native view can be re-created by the fullscreen/screen change, dropping the injected key bridge.
        try
        {
            await WebView.InvokeScript(WebViewKeyboardBridge.Script);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError($"Could not reinstall presentation keyboard bridge: {exception}");
        }
    }

    private void AttachHandlers()
    {
        _escapeHandler.ExitRequested += (_, _) => RaisePresentationExited();
        AddHandler(InputElement.KeyDownEvent, OnWindowPreviewKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        WebView.KeyDown += OnWindowPreviewKeyDown;
        WebView.NavigationStarted += OnWebViewNavigationStarted;
        WebView.NavigationCompleted += OnWebViewNavigationCompleted;
        WebView.NewWindowRequested += OnWebViewNewWindowRequested;
        WebView.WebMessageReceived += OnWebViewWebMessageReceived;
    }

    private void OnWindowPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || WebViewKeyboardBridge.TryHandleKey(WebView, e, _isScreenBlanked))
            return;

        // BUG-006: Always handle ESC to fully close the session, even if the WebView has already
        // processed it (e.g. to exit an embedded fullscreen video).
        if (_escapeHandler.TryHandle(e.Key))
            e.Handled = true;
    }

    private async void OnWebViewNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
            return;

        try
        {
            _isScreenBlanked = false;
            await WebView.InvokeScript(WebViewKeyboardBridge.Script);
            WebView.Focus();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError($"Could not install presentation keyboard bridge: {exception}");
        }
    }

    private void OnWebViewWebMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        if (WebViewKeyboardBridge.GetBlankState(e.Body) is bool isBlanked)
            _isScreenBlanked = isBlanked;

        if (WebViewKeyboardBridge.IsEscapeMessage(e.Body))
            _escapeHandler.TryHandle(Key.Escape);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (Uri.TryCreate(_participantUrl, UriKind.Absolute, out var uri))
            WebView.Source = uri;
    }

    private void OnWebViewNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        if (e.Request is null)
            return;

        if (!_navigationPolicy.ShouldOpenExternally(e.Request, WebView.Source))
            return;

        _browserLauncher.Open(e.Request);
        ExternalLinkNavigated?.Invoke(this, e.Request);
        e.Cancel = true;
    }

    private void OnWebViewNewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e)
    {
        if (e.Request is null)
            return;

        if (_navigationPolicy.ShouldOpenExternally(e.Request, WebView.Source))
        {
            _browserLauncher.Open(e.Request);
            ExternalLinkNavigated?.Invoke(this, e.Request);
            e.Handled = true;
            return;
        }

        WebView.Source = e.Request;
        e.Handled = true;
    }

    private void RaisePresentationExited()
    {
        if (_exitRaised)
            return;
        _exitRaised = true;
        PresentationExited?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // When the window exits fullscreen (e.g. via OS-level ESC on macOS, which never
        // reaches Avalonia's key routing), treat the state transition as an exit request.
        // The _isFullscreenRequested guard ensures we only react to transitions caused by
        // an explicit SetFullscreen() call, not unrelated window-state changes.
        if (_isFullscreenRequested
            && change.Property == WindowStateProperty
            && change.OldValue is WindowState oldState
            && change.NewValue is WindowState newState
            && oldState == WindowState.FullScreen
            && newState != WindowState.FullScreen)
        {
            _isFullscreenRequested = false;
            RaisePresentationExited();
        }
    }

}
