using SlideDevPresenter.App.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class BlankOverlayStateSynchronizerTests
{
    [Fact]
    public void Apply_MirrorsBothDirectionsAndSuppressesFeedbackAcrossMultipleToggles()
    {
        var state = new BlankOverlayStateSynchronizer();

        Assert.True(state.Apply(isBlanked: true, isWhite: false));  // presenter -> participant, black
        Assert.False(state.Apply(isBlanked: true, isWhite: false)); // participant echo -> presenter
        Assert.True(state.Apply(isBlanked: true, isWhite: true));   // participant -> presenter, white
        Assert.False(state.Apply(isBlanked: true, isWhite: true));  // presenter echo -> participant
        Assert.True(state.Apply(isBlanked: false, isWhite: false)); // presenter -> participant, restore
        Assert.False(state.Apply(isBlanked: false, isWhite: true)); // restore is normalized and idempotent

        Assert.False(state.IsBlanked);
        Assert.False(state.IsWhite);
    }
}
