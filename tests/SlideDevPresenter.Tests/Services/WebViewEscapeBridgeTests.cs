using SlideDevPresenter.App.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class WebViewEscapeBridgeTests
{
    [Theory]
    [InlineData("sdp:escape")]
    [InlineData("\"sdp:escape\"")]
    [InlineData(" sdp:escape ")]
    public void IsEscapeMessage_ForEscapePayload_ReturnsTrue(string body)
    {
        Assert.True(WebViewEscapeBridge.IsEscapeMessage(body));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{'key': 10}")]
    public void IsEscapeMessage_ForOtherPayload_ReturnsFalse(string? body)
    {
        Assert.False(WebViewEscapeBridge.IsEscapeMessage(body));
    }

    [Fact]
    public void Script_ForwardsEscapeViaInvokeCSharpAction()
    {
        Assert.Contains("'Escape'", WebViewEscapeBridge.Script);
        Assert.Contains($"invokeCSharpAction('{WebViewEscapeBridge.EscapeMessage}')", WebViewEscapeBridge.Script);
    }
}
