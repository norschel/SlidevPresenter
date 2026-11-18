using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

public interface ISlideDeckBuilder
{
    /// <summary>Returns true when sources, dependencies or build output changed since the last successful build.</summary>
    Task<bool> IsBuildRequiredAsync(PresentationProject project, CancellationToken cancellationToken = default);

    /// <summary>Always installs dependencies when needed and compiles the deck.</summary>
    Task BuildAsync(PresentationProject project, CancellationToken cancellationToken = default);

    /// <summary>Compiles only when <see cref="IsBuildRequiredAsync"/> reports a change; returns whether a build ran.</summary>
    Task<bool> BuildIfRequiredAsync(PresentationProject project, CancellationToken cancellationToken = default);
}
