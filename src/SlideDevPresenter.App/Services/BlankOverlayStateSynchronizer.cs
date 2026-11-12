namespace SlideDevPresenter.App.Services;

/// <summary>Tracks the mirrored overlay state and suppresses duplicate cross-surface updates.</summary>
internal sealed class BlankOverlayStateSynchronizer
{
    public bool IsBlanked { get; private set; }
    public bool IsWhite { get; private set; }

    public bool Apply(bool isBlanked, bool isWhite)
    {
        isWhite &= isBlanked;
        if (IsBlanked == isBlanked && IsWhite == isWhite)
            return false;

        IsBlanked = isBlanked;
        IsWhite = isWhite;
        return true;
    }
}
