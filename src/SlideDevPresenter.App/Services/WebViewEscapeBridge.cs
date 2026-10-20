namespace SlideDevPresenter.App.Services;

/// <summary>
/// Native WebViews keep OS keyboard focus, so Avalonia never sees ESC while the user is inside
/// the web content. This bridge injects a capture-phase keydown listener into the page that
/// forwards ESC to the host via <c>invokeCSharpAction</c>, which surfaces as <c>WebMessageReceived</c>.
/// </summary>
public static class WebViewEscapeBridge
{
    public const string EscapeMessage = "sdp:escape";

    public const string Script =
        "(function(){" +
        "if(window.__sdpEscapeBridge)return;" +
        "window.__sdpEscapeBridge=true;" +
        "window.addEventListener('keydown',function(e){" +
        "if(e.key!=='Escape'&&e.keyCode!==27)return;" +
        "try{if(typeof invokeCSharpAction==='function')invokeCSharpAction('" + EscapeMessage + "');" +
        "else if(window.external&&window.external.invokeCSharpAction)window.external.invokeCSharpAction('" + EscapeMessage + "');}" +
        "catch(_){}" +
        "},true);" +
        "})();";

    public static bool IsEscapeMessage(string? body) =>
        string.Equals(body?.Trim().Trim('"'), EscapeMessage, StringComparison.Ordinal);
}
