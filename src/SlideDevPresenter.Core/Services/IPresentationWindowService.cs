using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

public interface IPresentationWindowService
{
    /// <summary>Raised when the user exits the presentation (e.g. via ESC).</summary>
    event EventHandler? PresentationExited;

    /// <summary>Raised when the user navigates to an external URL from within the presentation.</summary>
    event EventHandler<Uri>? ExternalLinkNavigated;

    /// <summary>
    /// Opens the participant view (and optionally the presenter view). When <paramref name="targetDisplay"/> is
    /// provided the participant window is placed on that display; otherwise the service picks a display itself.
    /// </summary>
    Task OpenAsync(string participantUrl, string? presenterUrl, DisplayInfo? targetDisplay = null, CancellationToken cancellationToken = default);

    /// <summary>Closes all presentation windows.</summary>
    Task CloseAsync(CancellationToken cancellationToken = default);
}
