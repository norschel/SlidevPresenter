namespace SlideDevPresenter.App.Services;

/// <summary>Retains the authoritative participant overlay state across window replacement and navigation.</summary>
internal sealed class ParticipantBlankState
{
    public bool IsBlanked { get; private set; }
    public bool IsWhite { get; private set; }

    public void Set(bool isBlanked, bool isWhite)
    {
        IsBlanked = isBlanked;
        IsWhite = isBlanked && isWhite;
    }

    public void Replay(Action<bool, bool> apply) => apply(IsBlanked, IsWhite);
}
