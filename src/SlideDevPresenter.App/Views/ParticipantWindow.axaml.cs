using Avalonia.Controls;
using Avalonia.Input;

namespace SlideDevPresenter.App.Views;

public partial class ParticipantWindow : Window
{
    private readonly string _participantUrl;

    /// <summary>Raised when the user requests to exit the presentation (e.g. via ESC).</summary>
    public event EventHandler? PresentationExited;

    /// <summary>Parameterless constructor for the Avalonia designer.</summary>
    public ParticipantWindow()
    {
        _participantUrl = string.Empty;
        InitializeComponent();
    }

    public ParticipantWindow(string participantUrl)
    {
        _participantUrl = participantUrl;
        InitializeComponent();
        Opened += OnOpened;
    }

    /// <summary>Puts the window into fullscreen mode.</summary>
    public void SetFullscreen() => WindowState = WindowState.FullScreen;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            RaisePresentationExited();
        }
        base.OnKeyDown(e);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (Uri.TryCreate(_participantUrl, UriKind.Absolute, out var uri))
            WebView.Source = uri;
    }

    private void RaisePresentationExited() => PresentationExited?.Invoke(this, EventArgs.Empty);
}
