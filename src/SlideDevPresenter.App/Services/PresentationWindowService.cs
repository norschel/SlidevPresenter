using Avalonia;
using Avalonia.Threading;
using SlideDevPresenter.App.Views;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.App.Services;

public sealed class PresentationWindowService : IPresentationWindowService
{
    private readonly IDisplayService _displayService;
    private readonly ISettingsService _settingsService;
    private ParticipantWindow? _participantWindow;

    public event EventHandler? PresentationExited;
    public event EventHandler<Uri>? ExternalLinkNavigated;

    public PresentationWindowService(IDisplayService displayService, ISettingsService settingsService)
    {
        _displayService = displayService;
        _settingsService = settingsService;
    }

    public Task OpenAsync(string participantUrl, string? presenterUrl, CancellationToken cancellationToken = default)
    {
        Dispatcher.UIThread.Post(() => OpenOnUiThread(participantUrl));
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        Dispatcher.UIThread.Post(CloseOnUiThread);
        return Task.CompletedTask;
    }

    private void OpenOnUiThread(string participantUrl)
    {
        CloseOnUiThread();

        var settings = _settingsService.Settings.DisplayManagement;
        var displays = _displayService.GetDisplays();

        _participantWindow = new ParticipantWindow(participantUrl);
        _participantWindow.PresentationExited += OnParticipantWindowExited;
        _participantWindow.ExternalLinkNavigated += OnParticipantWindowExternalLinkNavigated;
        _participantWindow.Closed += OnParticipantWindowClosed;

        // On multi-display setups move the participant window to the secondary display before showing
        if (displays.Count >= 2 && settings.AutoDetectDisplays)
        {
            // Prefer the first non-primary display; if all report as primary fall back to index 1.
            var secondary = displays.FirstOrDefault(d => !d.IsPrimary) ?? displays[1];
            _participantWindow.Position = new PixelPoint(secondary.X + 10, secondary.Y + 10);
        }

        _participantWindow.Show();

        if (settings.FullscreenParticipantView)
            _participantWindow.SetFullscreen();
    }

    private void CloseOnUiThread()
    {
        if (_participantWindow is null)
            return;

        // Unsubscribe all handlers before Close() so that OnParticipantWindowClosed
        // does not treat the programmatic close as a user-initiated close.
        _participantWindow.PresentationExited -= OnParticipantWindowExited;
        _participantWindow.ExternalLinkNavigated -= OnParticipantWindowExternalLinkNavigated;
        _participantWindow.Closed -= OnParticipantWindowClosed;
        _participantWindow.Close();
        _participantWindow = null;
    }

    private void OnParticipantWindowExited(object? sender, EventArgs e)
    {
        CloseOnUiThread();
        PresentationExited?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Fired when the participant window is closed by the user (e.g. clicking the title-bar X
    /// button) rather than through the normal ESC/exit path. Ensures the session is stopped and
    /// the browser workspace is cleaned up even in that case.
    /// </summary>
    private void OnParticipantWindowClosed(object? sender, EventArgs e)
    {
        // CloseOnUiThread() unsubscribes this handler before calling Close(), so reaching
        // here means the user closed the window directly (not a programmatic close).
        _participantWindow = null;
        PresentationExited?.Invoke(this, EventArgs.Empty);
    }

    private void OnParticipantWindowExternalLinkNavigated(object? sender, Uri uri)
    {
        ExternalLinkNavigated?.Invoke(this, uri);
    }
}
