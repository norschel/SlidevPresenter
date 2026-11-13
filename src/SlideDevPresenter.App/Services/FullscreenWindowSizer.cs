using Avalonia;

namespace SlideDevPresenter.App.Services;

/// <summary>
/// Computes window sizes for platforms (macOS) where entering fullscreen keeps a borderless window's
/// previous size and centers it in a black fullscreen space.
/// </summary>
public static class FullscreenWindowSizer
{
    /// <summary>Converts screen bounds (in platform pixels) into a window size in device-independent units.</summary>
    public static Size GetScreenSize(PixelRect screenBounds, double scaling)
    {
        var scale = NormalizeScaling(scaling);
        return new Size(screenBounds.Width / scale, screenBounds.Height / scale);
    }

    /// <summary>
    /// Returns the height that keeps the window inside the screen when the platform pushed it down
    /// (e.g. below the camera notch), or <c>null</c> when the window already fits.
    /// </summary>
    public static double? GetClippedHeight(PixelRect screenBounds, PixelPoint windowPosition, double windowHeight, double scaling)
    {
        var scale = NormalizeScaling(scaling);
        var windowBottom = windowPosition.Y + (int)Math.Round(windowHeight * scale);
        if (windowBottom <= screenBounds.Bottom)
            return null;

        var top = Math.Clamp(windowPosition.Y, screenBounds.Y, screenBounds.Bottom);
        return (screenBounds.Bottom - top) / scale;
    }

    private static double NormalizeScaling(double scaling) => scaling > 0 ? scaling : 1;
}
