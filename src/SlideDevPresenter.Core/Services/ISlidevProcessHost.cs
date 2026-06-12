using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

public interface ISlidevProcessHost
{
    HostState State { get; }
    string? ParticipantUrl { get; }
    string? PresenterUrl { get; }
    int? Port { get; }
    string? ErrorMessage { get; }

    event EventHandler<HostStateChangedEventArgs>? StateChanged;

    Task StartAsync(PresentationProject project, int port, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public sealed class HostStateChangedEventArgs : EventArgs
{
    public HostState NewState { get; }
    public string? ErrorMessage { get; }

    public HostStateChangedEventArgs(HostState newState, string? errorMessage = null)
    {
        NewState = newState;
        ErrorMessage = errorMessage;
    }
}
