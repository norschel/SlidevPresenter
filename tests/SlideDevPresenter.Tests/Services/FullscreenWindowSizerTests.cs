using Avalonia;
using SlideDevPresenter.App.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class FullscreenWindowSizerTests
{
    [Theory]
    [InlineData(1.0, 1512, 982)]
    [InlineData(2.0, 756, 491)]
    [InlineData(0.0, 1512, 982)]
    public void GetScreenSize_ConvertsScreenBoundsToDeviceIndependentSize(double scaling, double width, double height)
    {
        var size = FullscreenWindowSizer.GetScreenSize(new PixelRect(1920, 0, 1512, 982), scaling);

        Assert.Equal(new Size(width, height), size);
    }

    [Fact]
    public void GetClippedHeight_WindowPushedBelowNotch_TrimsOverflow()
    {
        var height = FullscreenWindowSizer.GetClippedHeight(
            new PixelRect(1920, 0, 1512, 982), new PixelPoint(1920, 33), windowHeight: 982, scaling: 1);

        Assert.Equal(949, height);
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(30, 1050)]
    public void GetClippedHeight_WindowInsideScreen_ReturnsNull(int top, double windowHeight)
    {
        var height = FullscreenWindowSizer.GetClippedHeight(
            new PixelRect(0, 0, 1920, 1080), new PixelPoint(0, top), windowHeight, scaling: 1);

        Assert.Null(height);
    }
}
