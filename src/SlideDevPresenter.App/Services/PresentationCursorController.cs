using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace SlideDevPresenter.App.Services;

/// <summary>Hides only a presentation WebView, including its native DOM airspace.</summary>
public sealed class PresentationCursorController : IDisposable
{
    private readonly NativeWebView _webView;
    private readonly PresentationCursorState _state = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _timer = new() { Interval = PresentationCursorState.InactivityTimeout };
    private readonly Cursor? _originalCursor;
    private readonly Cursor _hiddenCursor = new(StandardCursorType.None);

    public const string Script = """
        (function(){
            if(window.__sdpCursorBridge)return;
            window.__sdpCursorBridge=true;
            let style=null,active=false;
            function notify(message){
                if(typeof invokeCSharpAction==='function')invokeCSharpAction(message);
                else if(window.external&&window.external.invokeCSharpAction)window.external.invokeCSharpAction(message);
            }
            window.__sdpSetCursor=function(enabled,hidden){
                active=enabled;
                if(!enabled||!hidden){if(style){style.remove();style=null;}return;}
                if(!style){
                    style=document.createElement('style');
                    style.textContent='html,body,body *{cursor:none!important;}';
                    document.documentElement.appendChild(style);
                }
            };
            window.addEventListener('mousemove',function(){
                if(!active)return;
                window.__sdpSetCursor(true,false);
                notify('sdp:cursor-move');
            },true);
        })();
        """;

    public PresentationCursorController(NativeWebView webView)
    {
        _webView = webView;
        _originalCursor = webView.Cursor;
        _timer.Tick += OnTick;
        webView.PointerMoved += OnPointerMoved;
        webView.WebMessageReceived += OnMessage;
    }

    public void SetActive(bool active)
    {
        if (active == _state.IsActive) return;
        if (active) { _state.Start(_clock.Elapsed); RestartTimeout(); }
        else { _state.Stop(); _timer.Stop(); }
        Apply();
    }

    public void PageLoaded() => Apply();

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        var hidden = _state.IsHidden;
        _state.Tick(_clock.Elapsed);
        RestartTimeout();
        if (hidden != _state.IsHidden) Apply();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e) => Move();

    private void Move()
    {
        if (!_state.IsActive) return;
        var hidden = _state.IsHidden;
        _state.Move(_clock.Elapsed);
        RestartTimeout();
        if (hidden) Apply();
    }

    private void OnMessage(object? sender, WebMessageReceivedEventArgs e)
    {
        if (!_state.IsActive) return;
        switch (e.Body?.Trim().Trim('"'))
        {
            case "sdp:cursor-move": Move(); break;
            case "sdp:cursor-toggle": _state.Toggle(_clock.Elapsed); RestartTimeout(); Apply(); break;
        }
    }

    private void RestartTimeout()
    {
        _timer.Stop();
        if (_state.GetRemainingInactivity(_clock.Elapsed) is { } remaining)
        {
            _timer.Interval = remaining;
            _timer.Start();
        }
    }

    private async void Apply()
    {
        _webView.Cursor = _state.IsHidden ? _hiddenCursor : _originalCursor;
        try
        {
            await _webView.InvokeScript(Script + $"window.__sdpSetCursor({_state.IsActive.ToString().ToLowerInvariant()},{_state.IsHidden.ToString().ToLowerInvariant()});");
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Could not synchronize presentation cursor: {exception}");
        }
    }

    public void Dispose()
    {
        SetActive(false);
        _timer.Stop();
        _timer.Tick -= OnTick;
        _webView.PointerMoved -= OnPointerMoved;
        _webView.WebMessageReceived -= OnMessage;
        _hiddenCursor.Dispose();
    }
}
