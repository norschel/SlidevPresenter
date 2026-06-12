using Avalonia.Input;

namespace SlideDevPresenter.App.Services;

public sealed class PresentationEscapeHandler
{
    public event EventHandler? ExitRequested;

    public bool TryHandle(Key key)
    {
        if (key != Key.Escape)
            return false;

        ExitRequested?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
