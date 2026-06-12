using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.App.Services;

public sealed class AvaloniaDisplayService : IDisplayService
{
    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return [];

        var screens = desktop.MainWindow?.Screens?.All;
        if (screens is null)
            return [];

        var result = new List<DisplayInfo>(screens.Count);
        for (var i = 0; i < screens.Count; i++)
        {
            var screen = screens[i];
            result.Add(new DisplayInfo(
                Index: i,
                IsPrimary: screen.IsPrimary,
                X: screen.Bounds.X,
                Y: screen.Bounds.Y,
                Width: screen.Bounds.Width,
                Height: screen.Bounds.Height));
        }
        return result.AsReadOnly();
    }
}
