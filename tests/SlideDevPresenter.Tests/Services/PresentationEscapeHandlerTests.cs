using Avalonia.Input;
using SlideDevPresenter.App.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class PresentationEscapeHandlerTests
{
    [Fact]
    public void TryHandle_WithEscape_RaisesExitRequested()
    {
        var handler = new PresentationEscapeHandler();
        var raised = false;
        handler.ExitRequested += (_, _) => raised = true;

        var handled = handler.TryHandle(Key.Escape);

        Assert.True(handled);
        Assert.True(raised);
    }

    [Fact]
    public void TryHandle_WithNonEscape_DoesNothing()
    {
        var handler = new PresentationEscapeHandler();
        var raised = false;
        handler.ExitRequested += (_, _) => raised = true;

        var handled = handler.TryHandle(Key.Enter);

        Assert.False(handled);
        Assert.False(raised);
    }
}
