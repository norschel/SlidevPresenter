using System.Text.Json;
using SlideDevPresenter.App.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class WebViewDiagnosticTests
{
    [Theory]
    [InlineData("NotAllowedError", "The operation is not allowed")]
    [InlineData("AbortError", "The fetch was aborted")]
    public void GetActionableDiagnostic_ExplainsPermissionAndFetchFailures(string name, string message)
    {
        var body = $$"""{"type":"sdp:diagnostic","name":"{{name}}","message":"{{message}}"}""";

        var diagnostic = WebViewKeyboardBridge.GetActionableDiagnostic(body);

        Assert.NotNull(diagnostic);
        Assert.Contains("Slidev", diagnostic);
        Assert.Contains(name == "NotAllowedError" ? "permission" : "refresh", diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetActionableDiagnostic_IgnoresUnrelatedWebPageMessages()
    {
        Assert.Null(WebViewKeyboardBridge.GetActionableDiagnostic("""{"type":"other","name":"Error"}"""));
        Assert.Null(WebViewKeyboardBridge.GetActionableDiagnostic("""{"type":"sdp:diagnostic","name":"TypeError"}"""));
        Assert.Null(WebViewKeyboardBridge.GetActionableDiagnostic("""{"type":1,"name":[]}"""));
        Assert.Null(WebViewKeyboardBridge.GetActionableDiagnostic("""[{"type":"sdp:diagnostic"}]"""));
        Assert.Null(WebViewKeyboardBridge.GetActionableDiagnostic("not json"));
    }

    [Fact]
    public void GetActionableDiagnostic_UnwrapsJsonStringPayload()
    {
        var body = JsonSerializer.Serialize("""{"type":"sdp:diagnostic","name":"AbortError","message":"The fetch was aborted"}""");

        var diagnostic = WebViewKeyboardBridge.GetActionableDiagnostic(body);

        Assert.NotNull(diagnostic);
        Assert.Contains("fetch was canceled", diagnostic);
    }
}
