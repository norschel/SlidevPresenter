using System.Diagnostics;

namespace SlideDevPresenter.Infrastructure.Services;

/// <summary>
/// Adapts <see cref="System.Diagnostics.Process"/> to <see cref="IRunningProcess"/>.
/// </summary>
internal sealed class RunningProcessAdapter : IRunningProcess
{
    private readonly Process _process;
    private bool _started;

    public bool HasExited => !_started || _process.HasExited;

    public event EventHandler<string?>? OutputDataReceived;
    public event EventHandler? Exited
    {
        add => _process.Exited += value;
        remove => _process.Exited -= value;
    }

    public RunningProcessAdapter(Process process)
    {
        _process = process;
        _process.OutputDataReceived += (s, e) => OutputDataReceived?.Invoke(s, e.Data);
    }

    public void Start()
    {
        _started = _process.Start();
        if (!_started)
            throw new InvalidOperationException("Could not start the Slidev process.");
        _process.StandardInput.Close();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public void Kill(bool entireProcessTree = true) => _process.Kill(entireProcessTree);
    public Task WaitForExitAsync(CancellationToken cancellationToken) => _process.WaitForExitAsync(cancellationToken);
    public void Dispose() => _process.Dispose();
}
