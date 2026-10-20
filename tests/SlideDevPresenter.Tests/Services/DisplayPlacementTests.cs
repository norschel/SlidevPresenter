using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Tests.Services;

public class DisplayPlacementTests
{
    private static readonly DisplayInfo Primary = new(0, true, 0, 0, 1920, 1080);
    private static readonly DisplayInfo Second = new(1, false, 1920, 0, 2560, 1440);
    private static readonly DisplayInfo Third = new(2, false, 4480, 0, 1920, 1080);

    [Fact]
    public void PickAlternateDisplay_PrefersPrimary_WhenParticipantTakesSecondary()
    {
        var result = DisplayPlacement.PickAlternateDisplay([Primary, Second, Third], Second);

        Assert.Equal(Primary, result);
    }

    [Fact]
    public void PickAlternateDisplay_FallsBackToFirstOther_WhenParticipantTakesPrimary()
    {
        var result = DisplayPlacement.PickAlternateDisplay([Primary, Second, Third], Primary);

        Assert.Equal(Second, result);
    }

    [Fact]
    public void PickAlternateDisplay_ReturnsNull_WhenNoOtherDisplay()
    {
        Assert.Null(DisplayPlacement.PickAlternateDisplay([Primary], Primary));
    }

    [Theory]
    [InlineData(1920, 0, 2560, 1440, true)]
    [InlineData(0, 0, 1920, 1080, false)]
    [InlineData(1920, 0, 2560, 1400, false)]
    public void Matches_ComparesBoundsExactly(int x, int y, int width, int height, bool expected)
    {
        Assert.Equal(expected, DisplayPlacement.Matches(Second, x, y, width, height));
    }
}
