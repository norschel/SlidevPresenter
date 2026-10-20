using SlideDevPresenter.App.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class WebViewKeyboardBridgeTests
{
    [Theory]
    [InlineData("sdp:escape")]
    [InlineData("\"sdp:escape\"")]
    [InlineData(" sdp:escape ")]
    public void IsEscapeMessage_ForEscapePayload_ReturnsTrue(string body)
    {
        Assert.True(WebViewKeyboardBridge.IsEscapeMessage(body));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{'key': 10}")]
    public void IsEscapeMessage_ForOtherPayload_ReturnsFalse(string? body)
    {
        Assert.False(WebViewKeyboardBridge.IsEscapeMessage(body));
    }

    [Fact]
    public void Script_ForwardsEscapeViaInvokeCSharpAction()
    {
        Assert.Contains("'Escape'", WebViewKeyboardBridge.Script);
        Assert.Contains($"invokeCSharpAction('{WebViewKeyboardBridge.EscapeMessage}')", WebViewKeyboardBridge.Script);
    }

    [Fact]
    public void Script_IncludesEscapeHandlingAfterPresentationKeys()
    {
        Assert.EndsWith(WebViewKeyboardBridge.EscapeScript, WebViewKeyboardBridge.Script);
        Assert.True(WebViewKeyboardBridge.Script.IndexOf("if(blankScreen)", StringComparison.Ordinal)
            < WebViewKeyboardBridge.Script.IndexOf("if(e.key!=='Escape'", StringComparison.Ordinal));
    }

    [Fact]
    public void EscapeScript_DoesNotInstallPresentationKeysOnBrowserPages()
    {
        Assert.Contains($"invokeCSharpAction('{WebViewKeyboardBridge.EscapeMessage}')", WebViewKeyboardBridge.EscapeScript);
        Assert.DoesNotContain("__sdpPresentationKeys", WebViewKeyboardBridge.EscapeScript);
        Assert.DoesNotContain("ArrowRight", WebViewKeyboardBridge.EscapeScript);
    }
}
