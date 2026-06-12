using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SlideDevPresenter.App.Services;

namespace SlideDevPresenter.App.Views;

public partial class ParticipantWindow : Window
{
    private readonly string _participantUrl;
    private readonly IExternalBrowserLauncher _browserLauncher;
    private readonly WebViewNavigationPolicy _navigationPolicy;
    private readonly PresentationEscapeHandler _escapeHandler;

    /// <summary>Raised when the user requests to exit the presentation (e.g. via ESC).</summary>
    public event EventHandler? PresentationExited;

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
    public void SetFullscreen() => WindowState = WindowState.FullScreen;

    private void AttachHandlers()
    {
        _escapeHandler.ExitRequested += (_, _) => RaisePresentationExited();
        AddHandler(InputElement.KeyDownEvent, OnWindowPreviewKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        WebView.KeyDown += OnWindowPreviewKeyDown;
        WebView.NavigationStarted += OnWebViewNavigationStarted;
        WebView.NewWindowRequested += OnWebViewNewWindowRequested;
    }

    private void OnWindowPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        if (_escapeHandler.TryHandle(e.Key))
            e.Handled = true;
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
        e.Cancel = true;
    }

    private void OnWebViewNewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e)
    {
        if (e.Request is null)
            return;

        if (_navigationPolicy.ShouldOpenExternally(e.Request, WebView.Source))
        {
            _browserLauncher.Open(e.Request);
            e.Handled = true;
            return;
        }

        WebView.Source = e.Request;
        e.Handled = true;
    }

    private void RaisePresentationExited() => PresentationExited?.Invoke(this, EventArgs.Empty);
}
