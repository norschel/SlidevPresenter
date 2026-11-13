namespace SlideDevPresenter.App.Services;

public sealed class PresentationBlankStateChangedEventArgs(bool isBlanked, bool isWhite) : EventArgs
{
    public bool IsBlanked { get; } = isBlanked;
    public bool IsWhite { get; } = isWhite;
}
