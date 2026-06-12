using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

public interface ISourceScanner
{
    /// <summary>
    /// Scans the given local root directory and returns all detected Slidev projects.
    /// </summary>
    IReadOnlyList<PresentationProject> ScanRoot(string rootPath);

    /// <summary>
    /// Returns true if the given directory appears to be a Slidev project.
    /// </summary>
    bool IsSlidevProject(string directoryPath);
}
