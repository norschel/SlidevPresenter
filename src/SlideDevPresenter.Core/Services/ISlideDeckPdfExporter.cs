using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

public interface ISlideDeckPdfExporter
{
    Task ExportPdfAsync(PresentationProject project, string outputPath, CancellationToken cancellationToken = default);
}
