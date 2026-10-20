namespace SlideDevPresenter.App.Services;

/// <summary>
/// Handles presentation keyboard shortcuts in native WebViews and forwards Escape to the host.
/// </summary>
public static class WebViewKeyboardBridge
{
    public const string EscapeMessage = "sdp:escape";

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

    public const string Script = """
        (function(){
            if(window.__sdpPresentationKeys)return;
            window.__sdpPresentationKeys=true;
            let blankScreen=null;
            window.addEventListener('keydown',function(event){
                if(!event.isTrusted)return;
                function consume(){
                    event.preventDefault();
                    event.stopImmediatePropagation();
                }
                if(blankScreen){
                    consume();
                    if(event.repeat)return;
                    blankScreen.remove();
                    blankScreen=null;
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
                }else if(key==='n'){
                    consume();
                    window.dispatchEvent(new KeyboardEvent('keydown',{
                        key:'ArrowRight',code:'ArrowRight',keyCode:39,which:39,bubbles:true
                    }));
                }
            },true);
        })();
        """ + EscapeScript;

    public static bool IsEscapeMessage(string? body) =>
        string.Equals(body?.Trim().Trim('"'), EscapeMessage, StringComparison.Ordinal);
}
