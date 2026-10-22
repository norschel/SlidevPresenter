using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

public interface IGitRepositoryService
{
    Task<bool> IsGitAvailableAsync();
    Task<IReadOnlyList<string>> GetBranchesAsync(PresentationSource source);
    /// <summary>Returns null when the source folder is not inside a Git working tree; empty for a detached HEAD.</summary>
    Task<string?> GetCurrentBranchAsync(PresentationSource source);
    Task<RepositoryPreparation> PrepareAsync(PresentationSource source, Func<string, Task<bool>> confirmUpdate);
}

public sealed record RepositoryPreparation(string? Directory, string? Message, bool BranchSwitchDeclined = false);