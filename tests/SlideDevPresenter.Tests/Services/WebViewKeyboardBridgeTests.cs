using Avalonia.Input;
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

    [Theory]
    [InlineData(Key.B, "b")]
    [InlineData(Key.W, "w")]
    [InlineData(Key.N, "n")]
    [InlineData(Key.P, "p")]
    [InlineData(Key.OemPeriod, ".")]
    [InlineData(Key.Decimal, ".")]
    public void GetKeyScript_ForPresentationKey_UsesSharedHandler(Key key, string value)
    {
        Assert.Contains("window.__sdpHandlePresentationKey", WebViewKeyboardBridge.GetKeyScript(key, KeyModifiers.None));
        Assert.Contains($"key:\"{value}\"", WebViewKeyboardBridge.GetKeyScript(key, KeyModifiers.None));
        Assert.True(WebViewKeyboardBridge.ShouldHandleKey(key, KeyModifiers.None, false));
    }

    [Theory]
    [InlineData(KeyModifiers.Control)]
    [InlineData(KeyModifiers.Alt)]
    [InlineData(KeyModifiers.Meta)]
    public void ShouldHandleKey_ForModifiedShortcut_OnlyConsumesWhileBlanked(KeyModifiers modifiers)
    {
        Assert.False(WebViewKeyboardBridge.ShouldHandleKey(Key.B, modifiers, false));
        Assert.Null(WebViewKeyboardBridge.GetKeyScript(Key.B, modifiers));
        Assert.True(WebViewKeyboardBridge.ShouldHandleKey(Key.B, modifiers, true));
    }

    [Theory]
    [InlineData(Key.Escape)]
    [InlineData(Key.Right)]
    [InlineData(Key.Space)]
    public void ShouldHandleKey_ForOtherKey_OnlyConsumesWhileBlanked(Key key)
    {
        Assert.False(WebViewKeyboardBridge.ShouldHandleKey(key, KeyModifiers.None, false));
        Assert.True(WebViewKeyboardBridge.ShouldHandleKey(key, KeyModifiers.None, true));
    }

    [Theory]
    [InlineData("sdp:blank", true)]
    [InlineData("\"sdp:blank\"", true)]
    [InlineData("sdp:restore", false)]
    [InlineData("sdp:escape", null)]
    public void GetBlankState_ForBridgeMessage_ReturnsExpectedState(string body, bool? expected)
    {
        Assert.Equal(expected, WebViewKeyboardBridge.GetBlankState(body));
    }
}
