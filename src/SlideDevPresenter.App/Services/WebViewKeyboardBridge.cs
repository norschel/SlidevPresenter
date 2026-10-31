using Avalonia.Input;
using Avalonia.Controls;
using System.Diagnostics;
using System.Text.Json;

namespace SlideDevPresenter.App.Services;

/// <summary>
/// Handles presentation keyboard shortcuts in native WebViews and forwards Escape to the host.
/// </summary>
public static class WebViewKeyboardBridge
{
    public const string EscapeMessage = "sdp:escape";
    public const string BlankMessage = "sdp:blank";
    public const string BlankWhiteMessage = "sdp:blank-white";
    public const string RestoreMessage = "sdp:restore";

    public const string EscapeScript =
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

    public const string DiagnosticsScript = """
        (function(){
            if(window.__sdpDiagnosticsBridge)return;
            window.__sdpDiagnosticsBridge=true;
            function notify(message){
                if(typeof invokeCSharpAction==='function')invokeCSharpAction(message);
                else if(window.external&&window.external.invokeCSharpAction)window.external.invokeCSharpAction(message);
            }
            window.addEventListener('unhandledrejection',function(event){
                const error=event.reason;
                const name=String(error&&error.name||'');
                if(name!=='NotAllowedError'&&name!=='AbortError')return;
                const message=String(error&&error.message||error||'').slice(0,240);
                notify(JSON.stringify({type:'sdp:diagnostic',name:name,message:message}));
            });
        })();
        """;

    public const string Script = """
        (function(){
            if(window.__sdpPresentationKeys)return;
            window.__sdpPresentationKeys=true;
            let blankScreen=null;
            function notify(message){
                if(typeof invokeCSharpAction==='function')invokeCSharpAction(message);
                else if(window.external&&window.external.invokeCSharpAction)window.external.invokeCSharpAction(message);
            }
            window.__sdpHandlePresentationKey=function(event){
                function consume(){
                    event.preventDefault();
                    event.stopImmediatePropagation();
                }
                if(blankScreen){
                    consume();
                    if(event.repeat)return;
                    blankScreen.remove();
                    blankScreen=null;
                    notify('sdp:restore');
                    return;
                }
                const target=event.target;
                if(event.ctrlKey||event.metaKey||event.altKey||
                    (target instanceof Element&&target.closest('input,textarea,select,[contenteditable]:not([contenteditable="false"])')))return;
                const key=event.key.toLowerCase();
                if(key==='.'||key==='b'||key==='w'){
                    consume();
                    if(event.repeat)return;
                    blankScreen=document.createElement('div');
                    blankScreen.style.cssText='position:fixed;inset:0;width:100%;height:100%;z-index:2147483647;pointer-events:auto;';
                    blankScreen.style.backgroundColor=key==='w'?'white':'black';
                    (document.fullscreenElement||document.body).appendChild(blankScreen);
                    notify(key==='w'?'sdp:blank-white':'sdp:blank');
                }else if(key==='n'||key==='p'){
                    consume();
                    const direction=key==='p'?'ArrowLeft':'ArrowRight';
                    const keyCode=key==='p'?37:39;
                    for(const type of ['keydown','keyup']){
                        document.dispatchEvent(new KeyboardEvent(type,{
                            key:direction,code:direction,keyCode:keyCode,which:keyCode,bubbles:true
                        }));
                    }
                }
            };
            window.addEventListener('keydown',window.__sdpHandlePresentationKey,true);
        })();
        """ + EscapeScript;

    public static bool ShouldHandleKey(Key key, KeyModifiers modifiers, bool isBlanked) =>
        isBlanked || ((modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0
            && key is Key.B or Key.W or Key.N or Key.P or Key.OemPeriod or Key.Decimal);

    public static bool TryHandleKey(NativeWebView webView, KeyEventArgs eventArgs, bool isBlanked)
    {
        if (eventArgs.Handled || !ShouldHandleKey(eventArgs.Key, eventArgs.KeyModifiers, isBlanked))
            return false;

        eventArgs.Handled = true;
        _ = DispatchKeyAsync(webView, eventArgs.Key, isBlanked ? KeyModifiers.None : eventArgs.KeyModifiers);
        return true;
    }

    public static async Task DispatchKeyAsync(NativeWebView webView, Key key, KeyModifiers modifiers, bool useFocusedElement = true)
    {
        try
        {
            await webView.InvokeScript(Script + GetKeyScript(key, modifiers, useFocusedElement));
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Could not dispatch presentation key: {exception}");
        }
    }

    public static string? GetKeyScript(Key key, KeyModifiers modifiers, bool useFocusedElement = true)
    {
        if ((modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
            return null;

        var value = key switch
        {
            Key.B => "b",
            Key.W => "w",
            Key.N => "n",
            Key.P => "p",
            Key.OemPeriod or Key.Decimal => ".",
            _ => key.ToString()
        };

        return "window.__sdpHandlePresentationKey?.({key:" + JsonSerializer.Serialize(value)
            + ",target:" + (useFocusedElement ? "document.activeElement" : "document.body")
            + ",preventDefault:function(){},stopImmediatePropagation:function(){}});";
    }

    public static bool IsEscapeMessage(string? body) =>
        string.Equals(body?.Trim().Trim('"'), EscapeMessage, StringComparison.Ordinal);

    public static bool IsWhiteBlankMessage(string? body) =>
        string.Equals(body?.Trim().Trim('"'), BlankWhiteMessage, StringComparison.Ordinal);

    public static bool? GetBlankState(string? body) => body?.Trim().Trim('"') switch
    {
        BlankMessage or BlankWhiteMessage => true,
        RestoreMessage => false,
        _ => null
    };

    /// <summary>Converts known permission and fetch rejections into user-facing recovery guidance.</summary>
    public static string? GetActionableDiagnostic(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("type", out var type)
                || type.ValueKind != JsonValueKind.String
                || type.GetString() != "sdp:diagnostic"
                || !root.TryGetProperty("name", out var nameElement)
                || nameElement.ValueKind != JsonValueKind.String)
                return null;

            var name = nameElement.GetString();
            var detail = root.TryGetProperty("message", out var messageElement)
                && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString()
                : null;
            if (!string.IsNullOrWhiteSpace(detail) && detail.Length > 160)
                detail = detail[..160];

            return name switch
            {
                "NotAllowedError" => $"Slidev could not obtain a browser permission{FormatDetail(detail)}. Check the browser or operating-system permission settings and keep the presenter view visible; SlideDevPresenter does not override the permission.",
                "AbortError" => $"A Slidev presenter fetch was canceled{FormatDetail(detail)}. This can happen during navigation; if the presenter stops updating, refresh or reload the presenter view, or restart the presentation with Retry.",
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string FormatDetail(string? detail) =>
        string.IsNullOrWhiteSpace(detail) ? string.Empty : $" ({detail})";
}
