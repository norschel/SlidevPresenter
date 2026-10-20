using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

/// <summary>Asks the user which display the participant view should be shown on.</summary>
public interface IDisplaySelector
{
    /// <summary>
    /// Returns the chosen display, or <c>null</c> when the user declined to open the participant window.
    /// Only called when more than one display is available.
    /// </summary>
    Task<DisplayInfo?> SelectDisplayAsync(IReadOnlyList<DisplayInfo> displays, CancellationToken cancellationToken = default);
}
