using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using SlideDevPresenter.App.Views;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.App.Services;

public sealed class PresentationWindowService : IPresentationWindowService
{
    private readonly IDisplayService _displayService;
    private readonly ISettingsService _settingsService;
    private ParticipantWindow? _participantWindow;
    private (PixelPoint Position, WindowState State)? _mainWindowOrigin;

    public event EventHandler? PresentationExited;
    public event EventHandler<Uri>? ExternalLinkNavigated;

    public PresentationWindowService(IDisplayService displayService, ISettingsService settingsService)
    {
        _displayService = displayService;
        _settingsService = settingsService;
    }

    public Task OpenAsync(string participantUrl, string? presenterUrl, DisplayInfo? targetDisplay = null, CancellationToken cancellationToken = default)
    {
        Dispatcher.UIThread.Post(() => OpenOnUiThread(participantUrl, targetDisplay));
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CloseOnUiThread();
            RestoreMainWindowPosition();
        });
        return Task.CompletedTask;
    }

    private void OpenOnUiThread(string participantUrl, DisplayInfo? targetDisplay)
    {
        CloseOnUiThread();

        var settings = _settingsService.Settings.DisplayManagement;

        _participantWindow = new ParticipantWindow(participantUrl);
        _participantWindow.PresentationExited += OnParticipantWindowExited;
        _participantWindow.ExternalLinkNavigated += OnParticipantWindowExternalLinkNavigated;
        _participantWindow.Closed += OnParticipantWindowClosed;

        var display = targetDisplay ?? PickDefaultDisplay(settings);
        if (display is not null)
        {
            MoveMainWindowOffDisplay(display);
            _participantWindow.Position = new PixelPoint(display.X + 10, display.Y + 10);
        }

        _participantWindow.Show();

        if (settings.FullscreenParticipantView)
            _participantWindow.SetFullscreen();
    }

    /// <summary>If the main window sits on the display the participant view is about to take over, relocate it.</summary>
    private void MoveMainWindowOffDisplay(DisplayInfo occupied)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            return;

        var currentScreen = mainWindow.Screens.ScreenFromWindow(mainWindow);
        if (currentScreen is null)
            return;

        var bounds = currentScreen.Bounds;
        if (!DisplayPlacement.Matches(occupied, bounds.X, bounds.Y, bounds.Width, bounds.Height))
            return;

        var alternate = DisplayPlacement.PickAlternateDisplay(_displayService.GetDisplays(), occupied);
        if (alternate is null)
            return;

        // Keep the first origin across re-opens within the same session so Restore returns to where the user started.
        _mainWindowOrigin ??= (mainWindow.Position, mainWindow.WindowState);

        // A maximized/fullscreen window cannot be repositioned; normalize, move, then restore the state.
        var previousState = mainWindow.WindowState;
        if (previousState != WindowState.Normal)
            mainWindow.WindowState = WindowState.Normal;

        mainWindow.Position = new PixelPoint(alternate.X + 40, alternate.Y + 40);

        if (previousState is WindowState.Maximized or WindowState.FullScreen)
            mainWindow.WindowState = previousState;
    }

    /// <summary>Moves the main window back to where it was before the participant view displaced it.</summary>
    private void RestoreMainWindowPosition()
    {
        if (_mainWindowOrigin is not { } origin)
            return;

        _mainWindowOrigin = null;

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            return;

        if (mainWindow.WindowState != WindowState.Normal)
            mainWindow.WindowState = WindowState.Normal;

        mainWindow.Position = origin.Position;

        if (origin.State is WindowState.Maximized or WindowState.FullScreen)
            mainWindow.WindowState = origin.State;
    }

    private DisplayInfo? PickDefaultDisplay(DisplayManagementSettings settings)
    {
        var displays = _displayService.GetDisplays();

        // On multi-display setups move the participant window to the secondary display before showing
        if (displays.Count < 2 || !settings.AutoDetectDisplays)
            return null;

        // Prefer the first non-primary display; if all report as primary fall back to index 1.
        return displays.FirstOrDefault(d => !d.IsPrimary) ?? displays[1];
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
        RestoreMainWindowPosition();
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
        RestoreMainWindowPosition();
        PresentationExited?.Invoke(this, EventArgs.Empty);
    }

    private void OnParticipantWindowExternalLinkNavigated(object? sender, Uri uri)
    {
        ExternalLinkNavigated?.Invoke(this, uri);
    }
}
