using SlideDevPresenter.App.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class PresentationCursorStateTests
{
    [Fact]
    public void Start_HidesImmediately_AndStopRestores()
    {
        var state = new PresentationCursorState();
        state.Start(TimeSpan.Zero);
        Assert.True(state.IsActive);
        Assert.True(state.IsHidden);
        state.Stop();
        Assert.False(state.IsActive);
        Assert.False(state.IsHidden);
        state.Tick(TimeSpan.FromMinutes(2));
        state.Move(TimeSpan.FromMinutes(2));
        state.Toggle(TimeSpan.FromMinutes(2));
        Assert.False(state.IsHidden);
    }

    [Theory]
    [InlineData(59999, false)]
    [InlineData(60000, true)]
    [InlineData(60001, true)]
    public void Movement_HidesAtExactSixtySecondThreshold(int elapsedMilliseconds, bool hidden)
    {
        var state = new PresentationCursorState();
        state.Start(TimeSpan.Zero);
        state.Move(TimeSpan.FromSeconds(10));
        Assert.False(state.IsHidden);
        state.Tick(TimeSpan.FromSeconds(10) + TimeSpan.FromMilliseconds(elapsedMilliseconds));
        Assert.Equal(hidden, state.IsHidden);
    }

    [Fact]
    public void Toggle_UsesCurrentVisibility_ThenMovementAndTimeoutContinue()
    {
        var state = new PresentationCursorState();
        state.Start(TimeSpan.Zero);
        state.Toggle(TimeSpan.FromSeconds(1));
        Assert.False(state.IsHidden);
        state.Toggle(TimeSpan.FromSeconds(2));
        Assert.True(state.IsHidden);
        state.Move(TimeSpan.FromSeconds(3));
        Assert.False(state.IsHidden);
        state.Toggle(TimeSpan.FromSeconds(4));
        Assert.True(state.IsHidden);
        state.Toggle(TimeSpan.FromSeconds(5));
        state.Tick(TimeSpan.FromSeconds(64));
        Assert.False(state.IsHidden);
        state.Tick(TimeSpan.FromSeconds(65));
        Assert.True(state.IsHidden);
    }

    [Fact]
    public void Restart_DoesNotInheritPreviousInactivityDeadline()
    {
        var state = new PresentationCursorState();
        state.Start(TimeSpan.Zero);
        state.Stop();
        state.Start(TimeSpan.FromMinutes(5));
        Assert.True(state.IsHidden);
        state.Move(TimeSpan.FromMinutes(5));
        state.Tick(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(59));
        Assert.False(state.IsHidden);
    }

    [Fact]
    public void EarlyTick_LeavesRemainingDeadline_AndNextTickHidesWithoutMovement()
    {
        var state = new PresentationCursorState();
        state.Start(TimeSpan.Zero);
        state.Move(TimeSpan.FromSeconds(10));
        var early = TimeSpan.FromMilliseconds(69999);
        state.Tick(early);
        Assert.False(state.IsHidden);
        var remaining = state.GetRemainingInactivity(early);
        Assert.Equal(TimeSpan.FromMilliseconds(1), remaining);
        state.Tick(early + remaining!.Value);
        Assert.True(state.IsHidden);
        Assert.Null(state.GetRemainingInactivity(TimeSpan.FromSeconds(70)));
    }

    [Fact]
    public void RemainingDeadline_MovementAndToggleReset_StopDisarms()
    {
        var state = new PresentationCursorState();
        state.Start(TimeSpan.Zero);
        Assert.Null(state.GetRemainingInactivity(TimeSpan.Zero));
        state.Toggle(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(60), state.GetRemainingInactivity(TimeSpan.FromSeconds(2)));
        state.Move(TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.FromSeconds(60), state.GetRemainingInactivity(TimeSpan.FromSeconds(30)));
        state.Stop();
        Assert.Null(state.GetRemainingInactivity(TimeSpan.FromSeconds(31)));
    }
}
