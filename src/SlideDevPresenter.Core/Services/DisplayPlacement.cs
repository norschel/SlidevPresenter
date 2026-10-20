using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

public static class DisplayPlacement
{
    /// <summary>Returns true when <paramref name="display"/> describes the same screen as the given bounds.</summary>
    public static bool Matches(DisplayInfo display, int x, int y, int width, int height) =>
        display.X == x && display.Y == y && display.Width == width && display.Height == height;

    /// <summary>
    /// Picks the display the main window should move to when the participant view takes over
    /// <paramref name="occupied"/>: the primary display if free, otherwise the first other display.
    /// </summary>
    public static DisplayInfo? PickAlternateDisplay(IReadOnlyList<DisplayInfo> displays, DisplayInfo occupied) =>
        displays
            .Where(d => d.Index != occupied.Index)
            .OrderByDescending(d => d.IsPrimary)
            .ThenBy(d => d.Index)
            .FirstOrDefault();
}
