using System.Diagnostics;

namespace SlideDevPresenter.Infrastructure.Services;

/// <summary>
/// Adapts <see cref="System.Diagnostics.Process"/> to <see cref="IRunningProcess"/>.
/// </summary>
internal sealed class RunningProcessAdapter : IRunningProcess
{
    private readonly Process _process;

    public bool HasExited => _process.HasExited;

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

    public void Kill(bool entireProcessTree = true) => _process.Kill(entireProcessTree);
}
