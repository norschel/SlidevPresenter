using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
