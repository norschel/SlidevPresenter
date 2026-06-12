using Microsoft.Extensions.Logging.Abstractions;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.Tests.Services;

/// <summary>
/// Controllable fake that implements IRunningProcess for deterministic host tests.
/// </summary>
internal sealed class FakeRunningProcess : IRunningProcess
{
    private bool _hasExited;
    public bool HasExited => _hasExited;

    public event EventHandler<string?>? OutputDataReceived;
    public event EventHandler? Exited;

    public void Kill(bool entireProcessTree = true) { /* no-op: caller manages transition */ }

    public void SimulateOutput(string line) => OutputDataReceived?.Invoke(this, line);

    public void SimulateExit()
    {
        _hasExited = true;
        Exited?.Invoke(this, EventArgs.Empty);
    }
}

public class SlidevProcessHostTests
{
    private static PresentationProject MakeProject() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Talk",
        SourceType = PresentationSourceType.LocalProject,
        Location = "/tmp/test-talk",
        SlidesFilePath = "/tmp/test-talk/slides.md"
    };

    private static (SlidevProcessHost host, FakeRunningProcess fake) CreateHost()
    {
        var fake = new FakeRunningProcess();
        var host = new SlidevProcessHost(NullLogger<SlidevProcessHost>.Instance, _ => fake);
        return (host, fake);
    }

    // ── Initial state ─────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsIdle()
    {
        var (host, _) = CreateHost();
        Assert.Equal(HostState.Idle, host.State);
        Assert.Null(host.Port);
        Assert.Null(host.ParticipantUrl);
        Assert.Null(host.PresenterUrl);
        Assert.Null(host.ErrorMessage);
    }

    // ── StartAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_TransitionsToStarting()
    {
        var (host, _) = CreateHost();
        var states = new List<HostState>();
        host.StateChanged += (_, e) => states.Add(e.NewState);

        await host.StartAsync(MakeProject(), 3030);

        Assert.Equal(HostState.Starting, host.State);
        Assert.Contains(HostState.Starting, states);
    }

    [Fact]
    public async Task StartAsync_SetsPort()
    {
        var (host, _) = CreateHost();
        await host.StartAsync(MakeProject(), 4000);
        Assert.Equal(4000, host.Port);
    }

    [Fact]
    public async Task StartAsync_Throws_WhenAlreadyStarting()
    {
        var (host, _) = CreateHost();
        await host.StartAsync(MakeProject(), 3030);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.StartAsync(MakeProject(), 3030));
    }

    [Fact]
    public async Task StartAsync_Throws_WhenAlreadyRunning()
    {
        var (host, fake) = CreateHost();
        await host.StartAsync(MakeProject(), 3030);
        fake.SimulateOutput("  ➜  Local:   http://localhost:3030/");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.StartAsync(MakeProject(), 3031));
    }

    // ── Running transition ────────────────────────────────────────────────

    [Fact]
    public async Task OutputWithLocalUrl_TransitionsToRunning()
    {
        var (host, fake) = CreateHost();
        await host.StartAsync(MakeProject(), 3030);

        fake.SimulateOutput("  ➜  Local:   http://localhost:3030/");

        Assert.Equal(HostState.Running, host.State);
    }

    [Fact]
    public async Task OutputWithLocalUrl_SetsParticipantAndPresenterUrls()
    {
        var (host, fake) = CreateHost();
        await host.StartAsync(MakeProject(), 3030);

        fake.SimulateOutput("  ➜  Local:   http://localhost:3030/");

        Assert.Equal("http://localhost:3030/", host.ParticipantUrl);
        Assert.Equal("http://localhost:3030/presenter/", host.PresenterUrl);
    }

    [Fact]
    public async Task OutputWithoutLocalUrl_DoesNotTransition()
    {
        var (host, fake) = CreateHost();
        await host.StartAsync(MakeProject(), 3030);

        fake.SimulateOutput("some unrelated output line");

        Assert.Equal(HostState.Starting, host.State);
    }

    // ── StopAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task StopAsync_FromStarting_TransitionsToIdle()
    {
        var (host, _) = CreateHost();
        await host.StartAsync(MakeProject(), 3030);
        await host.StopAsync();

        Assert.Equal(HostState.Idle, host.State);
    }

    [Fact]
    public async Task StopAsync_FromRunning_TransitionsToIdle()
    {
        var (host, fake) = CreateHost();
        await host.StartAsync(MakeProject(), 3030);
        fake.SimulateOutput("  ➜  Local:   http://localhost:3030/");

        await host.StopAsync();

        Assert.Equal(HostState.Idle, host.State);
    }

    [Fact]
    public async Task StopAsync_ClearsPortAndUrls()
    {
        var (host, fake) = CreateHost();
        await host.StartAsync(MakeProject(), 3030);
        fake.SimulateOutput("  ➜  Local:   http://localhost:3030/");

        await host.StopAsync();

        Assert.Null(host.Port);
        Assert.Null(host.ParticipantUrl);
        Assert.Null(host.PresenterUrl);
    }

    // ── Unexpected process exit ───────────────────────────────────────────

    [Fact]
    public async Task UnexpectedExit_FromRunning_TransitionsToError()
    {
        var (host, fake) = CreateHost();
        await host.StartAsync(MakeProject(), 3030);
        fake.SimulateOutput("  ➜  Local:   http://localhost:3030/");

        fake.SimulateExit();

        Assert.Equal(HostState.Error, host.State);
        Assert.NotNull(host.ErrorMessage);
    }

    [Fact]
    public async Task UnexpectedExit_FromStarting_TransitionsToError()
    {
        var (host, fake) = CreateHost();
        await host.StartAsync(MakeProject(), 3030);

        fake.SimulateExit();

        Assert.Equal(HostState.Error, host.State);
    }

    [Fact]
    public async Task StopAsync_AfterUnexpectedExit_TransitionsToIdle()
    {
        var (host, fake) = CreateHost();
        await host.StartAsync(MakeProject(), 3030);
        fake.SimulateExit();

        await host.StopAsync();

        Assert.Equal(HostState.Idle, host.State);
    }

    // ── StateChanged event ────────────────────────────────────────────────

    [Fact]
    public async Task StateChanged_ErrorEvent_CarriesErrorMessage()
    {
        var (host, fake) = CreateHost();
        HostStateChangedEventArgs? lastEvent = null;
        host.StateChanged += (_, e) => lastEvent = e;

        await host.StartAsync(MakeProject(), 3030);
        fake.SimulateExit();

        Assert.NotNull(lastEvent);
        Assert.Equal(HostState.Error, lastEvent.NewState);
        Assert.NotNull(lastEvent.ErrorMessage);
    }

    [Fact]
    public async Task StateChanged_IdleEvent_HasNullErrorMessage()
    {
        var (host, _) = CreateHost();
        HostStateChangedEventArgs? lastEvent = null;
        host.StateChanged += (_, e) => lastEvent = e;

        await host.StartAsync(MakeProject(), 3030);
        await host.StopAsync();

        Assert.NotNull(lastEvent);
        Assert.Equal(HostState.Idle, lastEvent.NewState);
        Assert.Null(lastEvent.ErrorMessage);
    }

    // ── Process factory failure ───────────────────────────────────────────

    [Fact]
    public async Task StartAsync_WhenFactoryThrows_TransitionsToError()
    {
        var host = new SlidevProcessHost(
            NullLogger<SlidevProcessHost>.Instance,
            _ => throw new InvalidOperationException("npx not found"));

        var states = new List<HostState>();
        host.StateChanged += (_, e) => states.Add(e.NewState);

        await host.StartAsync(MakeProject(), 3030);

        Assert.Equal(HostState.Error, host.State);
        Assert.Contains(HostState.Error, states);
        Assert.Equal("npx not found", host.ErrorMessage);
    }
}
