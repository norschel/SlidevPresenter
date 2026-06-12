using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

public interface IDisplayService
{
    IReadOnlyList<DisplayInfo> GetDisplays();
}
