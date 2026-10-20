using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

public interface ISlideDeckBuilder
{
    Task BuildAsync(PresentationProject project, CancellationToken cancellationToken = default);
}