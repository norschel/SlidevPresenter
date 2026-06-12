using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

public interface ISlideDeckMetadataReader
{
    Task<SlideDeckMetadata> ReadAsync(PresentationProject project, CancellationToken cancellationToken = default);
}
