using SlideDevPresenter.App.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class ParticipantBlankStateTests
{
    [Fact]
    public void Replay_BlankedStateToReplacementParticipantWindow()
    {
        var state = new ParticipantBlankState();
        state.Set(isBlanked: true, isWhite: true);
        var previousWindow = new FakeParticipantSurface();
        state.Replay(previousWindow.SetBlankState);
        var replacementWindow = new FakeParticipantSurface();

        state.Replay(replacementWindow.SetBlankState);

        Assert.True(previousWindow.IsBlanked);
        Assert.True(previousWindow.IsWhite);
        Assert.True(replacementWindow.IsBlanked);
        Assert.True(replacementWindow.IsWhite);
    }

    private sealed class FakeParticipantSurface
    {
        public bool IsBlanked { get; private set; }
        public bool IsWhite { get; private set; }

        public void SetBlankState(bool isBlanked, bool isWhite)
        {
            IsBlanked = isBlanked;
            IsWhite = isBlanked && isWhite;
        }
    }
}
