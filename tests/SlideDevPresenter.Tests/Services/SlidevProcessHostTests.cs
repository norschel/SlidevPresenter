using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
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
    private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool HasExited => _hasExited;
    public Action? OnStart { get; set; }
    public int KillCount { get; private set; }
    public bool UseProcessSenderOnExit { get; set; }

    public event EventHandler<string?>? OutputDataReceived;
    public event EventHandler? Exited;

    public void Start() => OnStart?.Invoke();
    public void Kill(bool entireProcessTree = true)
    {
        KillCount++;
        SimulateExit();
    }
    public Task WaitForExitAsync(CancellationToken cancellationToken) => _exited.Task.WaitAsync(cancellationToken);
    public void Dispose() { }

    public void SimulateOutput(string line) => OutputDataReceived?.Invoke(this, line);

    public void SimulateExit()
    {
        _hasExited = true;
        _exited.TrySetResult();
        if (UseProcessSenderOnExit)
        {
            using var processSender = new Process();
            Exited?.Invoke(processSender, EventArgs.Empty);
        }
        else
        {
            Exited?.Invoke(this, EventArgs.Empty);
        }
    }
}

public class SlidevProcessHostTests
{
    private sealed class FakeBuilder : ISlideDeckBuilder
    {
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task BuildAsync(PresentationProject project, CancellationToken cancellationToken = default) => Completion.Task.WaitAsync(cancellationToken);
    }

    [Fact]
    public async Task StartAsync_WaitsForCompilationBeforeStartingServer()
    {
        var builder = new FakeBuilder();
        var processCount = 0;
        var host = new SlidevProcessHost(NullLogger<SlidevProcessHost>.Instance, _ =>
        {
            processCount++;
            return new FakeRunningProcess();
        }, builder);
        var startup = host.StartAsync(MakeProject(), 3030);
        Assert.Equal(HostState.Starting, host.State);
        Assert.Equal(0, processCount);

        builder.Completion.SetResult();
        await startup;

        Assert.Equal(1, processCount);
    }

    [Fact]
    public async Task StartAsync_CompilationFailure_DoesNotStartServer()
    {
        var builder = new FakeBuilder();
        var processCount = 0;
        var host = new SlidevProcessHost(NullLogger<SlidevProcessHost>.Instance, _ =>
        {
            processCount++;
            return new FakeRunningProcess();
        }, builder);
        builder.Completion.SetException(new InvalidOperationException("Compilation failed. Fix manually."));

        await host.StartAsync(MakeProject(), 3030);

        Assert.Equal(HostState.Error, host.State);
        Assert.Contains("Fix manually", host.ErrorMessage);
        Assert.Equal(0, processCount);
    }

    [Fact]
    public async Task StartAsync_CapturesReadyOutputEmittedDuringActivation()
    {
        var fake = new FakeRunningProcess();
        fake.OnStart = () => fake.SimulateOutput("  ➜  Local:   http://localhost:3030/");
        var host = new SlidevProcessHost(NullLogger<SlidevProcessHost>.Instance, _ => fake);

        await host.StartAsync(MakeProject(), 3030);

        Assert.Equal(HostState.Running, host.State);
        Assert.Equal("http://localhost:3030/", host.ParticipantUrl);
    }

    [Fact]
    public async Task StartAsync_CapturesExitDuringActivation()
    {
        var fake = new FakeRunningProcess();
        fake.OnStart = fake.SimulateExit;
        var host = new SlidevProcessHost(NullLogger<SlidevProcessHost>.Instance, _ => fake);

        await host.StartAsync(MakeProject(), 3030);

        Assert.Equal(HostState.Error, host.State);
        Assert.Contains("exited", host.ErrorMessage);
    }

    [Fact]
    public async Task StartAsync_SilentProcessTimesOutAndIsStopped()
    {
        var timeout = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new FakeRunningProcess();
        var host = new SlidevProcessHost(
            NullLogger<SlidevProcessHost>.Instance,
            _ => fake,
            readinessTimeout: TimeSpan.FromSeconds(45),
            delay: (_, _) => timeout.Task);

        await host.StartAsync(MakeProject(), 3030);
        var readiness = Assert.IsAssignableFrom<Task>(host.ReadinessTask);
        timeout.SetResult();
        await readiness;

        Assert.Equal(HostState.Error, host.State);
        Assert.Contains("ready URL", host.ErrorMessage);
        Assert.True(fake.HasExited);
        Assert.Equal(1, fake.KillCount);
    }

    [Fact]
    public async Task StopAsync_ProductionExitSender_DoesNotReportErrorOrCorruptNextStart()
    {
        var first = new FakeRunningProcess { UseProcessSenderOnExit = true };
        var second = new FakeRunningProcess();
        var processes = new Queue<FakeRunningProcess>([first, second]);
        var host = new SlidevProcessHost(NullLogger<SlidevProcessHost>.Instance, _ => processes.Dequeue());
        var states = new List<HostState>();
        host.StateChanged += (_, e) => states.Add(e.NewState);

        await host.StartAsync(MakeProject(), 3030);
        await host.StopAsync();

        Assert.DoesNotContain(HostState.Error, states);
        Assert.Equal(HostState.Idle, host.State);

        await host.StartAsync(MakeProject(), 3031);
        first.SimulateExit();

        Assert.Equal(HostState.Starting, host.State);
        Assert.Null(host.ErrorMessage);
        await host.StopAsync();
    }

    [Fact]
    public async Task ReadinessTimeout_RacingReadyOutput_CannotPublishRunningAfterTimeout()
    {
        var timeout = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var outputParsed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOutput = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new FakeRunningProcess();
        var host = new SlidevProcessHost(
            NullLogger<SlidevProcessHost>.Instance,
            _ => fake,
            readinessTimeout: TimeSpan.FromSeconds(45),
            delay: (_, _) => timeout.Task,
            afterReadyOutputParsed: () =>
            {
                outputParsed.TrySetResult();
                releaseOutput.Task.GetAwaiter().GetResult();
            });

        await host.StartAsync(MakeProject(), 3030);
        var output = Task.Run(() => fake.SimulateOutput("  ➜  Local:   http://localhost:3030/"));
        await outputParsed.Task;
        timeout.SetResult();
        await host.ReadinessTask!;
        releaseOutput.SetResult();
        await output;

        Assert.Equal(HostState.Error, host.State);
        Assert.Null(host.ParticipantUrl);
        Assert.Null(host.PresenterUrl);
    }

    [Fact]
    public async Task StopAsync_DuringCompilation_CancelsStartupWithoutStartingServer()
    {
        var builder = new FakeBuilder();
        var processCount = 0;
        var host = new SlidevProcessHost(NullLogger<SlidevProcessHost>.Instance, _ =>
        {
            processCount++;
            return new FakeRunningProcess();
        }, builder);
        var startup = host.StartAsync(MakeProject(), 3030);

        await host.StopAsync();
        await startup;

        Assert.Equal(HostState.Idle, host.State);
        Assert.Equal(0, processCount);
    }

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
    public async Task OutputWithPublicSlideShowUrl_TransitionsToRunningWithUrls()
    {
        var (host, fake) = CreateHost();
        await host.StartAsync(MakeProject(), 3030);

        fake.SimulateOutput("\u001b[2m  public slide show \u001b[22m  > \u001b[36mhttp://localhost:\u001b[1m3030\u001b[22m/\u001b[39m");

        Assert.Equal(HostState.Running, host.State);
        Assert.Equal("http://localhost:3030/", host.ParticipantUrl);
        Assert.Equal("http://localhost:3030/presenter/", host.PresenterUrl);
    }

    [Fact]
    public async Task OutputWithDifferentPort_UsesDetectedPort()
    {
        var (host, fake) = CreateHost();
        await host.StartAsync(MakeProject(), 3030);

        fake.SimulateOutput("  public slide show   > http://localhost:64848/");

        Assert.Equal(HostState.Running, host.State);
        Assert.Equal(64848, host.Port);
        Assert.Equal("http://localhost:64848/", host.ParticipantUrl);
        Assert.Equal("http://localhost:64848/presenter/", host.PresenterUrl);
    }

    [Fact]
    public async Task OutputWithPresenterModeUrl_DoesNotTransition()
    {
        var (host, fake) = CreateHost();
        await host.StartAsync(MakeProject(), 3030);

        fake.SimulateOutput("  presenter mode      > http://localhost:3030/presenter/");

        Assert.Equal(HostState.Starting, host.State);
        Assert.Null(host.ParticipantUrl);
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
