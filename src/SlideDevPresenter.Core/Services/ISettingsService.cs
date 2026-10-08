using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task ExportAsync(Stream destination, CancellationToken cancellationToken = default);
    Task ImportAsync(Stream source, CancellationToken cancellationToken = default);
}
