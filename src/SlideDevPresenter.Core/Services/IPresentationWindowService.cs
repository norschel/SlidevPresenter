namespace SlideDevPresenter.Core.Services;

public interface IPresentationWindowService
{
    /// <summary>Raised when the user exits the presentation (e.g. via ESC).</summary>
    event EventHandler? PresentationExited;

    /// <summary>Raised when the user navigates to an external URL from within the presentation.</summary>
    event EventHandler<Uri>? ExternalLinkNavigated;

    /// <summary>Opens the participant view (and optionally the presenter view) on the appropriate display(s).</summary>
    Task OpenAsync(string participantUrl, string? presenterUrl, CancellationToken cancellationToken = default);

    /// <summary>Closes all presentation windows.</summary>
    Task CloseAsync(CancellationToken cancellationToken = default);
}
