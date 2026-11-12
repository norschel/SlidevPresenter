using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.Tests.Services;

public class SlidevPdfExporterTests
{
    [Fact]
    public async Task ExportPdfAsync_PreCanceled_DoesNotLaunchProcess()
    {
        var launches = 0;
        var exporter = new SlidevPdfExporter(_ =>
        {
            launches++;
            return new FakePdfExportProcess();
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            exporter.ExportPdfAsync(MakeProject(), "talk.pdf", cancellation.Token));

        Assert.Equal(0, launches);
    }

    [Fact]
    public async Task ExportPdfAsync_Cancellation_KillsAndReapsProcessTree()
    {
        var fake = new FakePdfExportProcess();
        var exporter = new SlidevPdfExporter(_ => fake, TimeSpan.FromMinutes(1));
        using var cancellation = new CancellationTokenSource();
        var export = exporter.ExportPdfAsync(MakeProject(), "talk.pdf", cancellation.Token);
        await fake.WaitStarted.Task;

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => export);

        Assert.Equal(1, fake.KillCount);
        Assert.True(fake.HasExited);
        Assert.True(fake.Reaped);
    }

    [Fact]
    public async Task ExportPdfAsync_Timeout_KillsAndReapsProcessTree()
    {
        var fake = new FakePdfExportProcess();
        var exporter = new SlidevPdfExporter(_ => fake, TimeSpan.Zero);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            exporter.ExportPdfAsync(MakeProject(), "talk.pdf"));

        Assert.Contains("timed out", exception.Message);
        Assert.Equal(1, fake.KillCount);
        Assert.True(fake.HasExited);
        Assert.True(fake.Reaped);
    }

    [Fact]
    public async Task ExportPdfAsync_KillFailsWhileProcessActive_SurfacesCleanupFailure()
    {
        var fake = new FakePdfExportProcess { KillException = new IOException("Access denied.") };
        var exporter = new SlidevPdfExporter(_ => fake, TimeSpan.FromMinutes(1));
        using var cancellation = new CancellationTokenSource();
        var export = exporter.ExportPdfAsync(MakeProject(), "talk.pdf", cancellation.Token);
        await fake.WaitStarted.Task;

        cancellation.Cancel();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => export);

        Assert.Contains("may still be running", exception.Message);
        Assert.IsType<IOException>(exception.InnerException);
        Assert.False(fake.HasExited);
        Assert.False(fake.Reaped);
    }

    private static PresentationProject MakeProject() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Talk",
        SourceType = PresentationSourceType.LocalProject,
        Location = Path.Combine(Path.GetTempPath(), "talk"),
        SlidesFilePath = Path.Combine(Path.GetTempPath(), "talk", "slides.md")
    };

    private sealed class FakePdfExportProcess : IPdfExportProcess
    {
        private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool HasExited { get; private set; }
        public int ExitCode => 0;
        public int KillCount { get; private set; }
        public bool Reaped { get; private set; }
        public Exception? KillException { get; init; }
        public TaskCompletionSource WaitStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Start() { }
        public void Kill(bool entireProcessTree)
        {
            Assert.True(entireProcessTree);
            KillCount++;
            if (KillException is not null)
                throw KillException;
            HasExited = true;
            _exited.TrySetResult();
        }
        public Task<string> ReadStandardOutputAsync() => Task.FromResult(string.Empty);
        public Task<string> ReadStandardErrorAsync() => Task.FromResult(string.Empty);
        public async Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            WaitStarted.TrySetResult();
            await _exited.Task.WaitAsync(cancellationToken);
            Reaped = true;
        }
        public void Dispose() { }
    }

    [Fact]
    public void CreateStartInfo_UsesSlidevPdfExportAndPreservesPathsWithSpaces()
    {
        var project = new PresentationProject
        {
            Id = Guid.NewGuid(),
            Name = "Test Talk",
            SourceType = PresentationSourceType.LocalProject,
            Location = Path.Combine(Path.GetTempPath(), "my talk"),
            SlidesFilePath = Path.Combine(Path.GetTempPath(), "my talk", "slides.md")
        };
        var outputPath = Path.Combine(Path.GetTempPath(), "my output", "talk.pdf");

        var startInfo = SlidevPdfExporter.CreateStartInfo(project, outputPath);

        Assert.Equal(OperatingSystem.IsWindows() ? "cmd.exe" : "npx", startInfo.FileName);
        Assert.Equal(project.Location, startInfo.WorkingDirectory);
        var launcherArguments = OperatingSystem.IsWindows()
            ? new[] { "/d", "/c", "npx.cmd" }
            : [];
        Assert.Equal(
            launcherArguments.Concat([
                "slidev", "export", Path.GetFullPath(project.SlidesFilePath), "--format", "pdf", "--output", Path.GetFullPath(outputPath)
            ]),
            startInfo.ArgumentList);
    }

    [Fact]
    public void CreateStartInfo_ResolvesRelativeSlidePathsBeforeLaunching()
    {
        var project = new PresentationProject
        {
            Id = Guid.NewGuid(),
            Name = "Test Talk",
            SourceType = PresentationSourceType.LocalProject,
            Location = Path.Combine("talks", "demo"),
            SlidesFilePath = Path.Combine("talks", "demo", "slides.md")
        };

        var startInfo = SlidevPdfExporter.CreateStartInfo(project, Path.Combine(Path.GetTempPath(), "talk.pdf"));

        var slidePathArgumentIndex = OperatingSystem.IsWindows() ? 5 : 2;
        Assert.Equal(Path.GetFullPath(project.SlidesFilePath), startInfo.ArgumentList[slidePathArgumentIndex]);
    }

    [Fact]
    public void CreateStartInfo_RejectsEmptyOutputPath()
    {
        var project = new PresentationProject
        {
            Id = Guid.NewGuid(),
            Name = "Test Talk",
            SourceType = PresentationSourceType.LocalProject,
            Location = "/tmp/test"
        };

        Assert.Throws<ArgumentException>(() => SlidevPdfExporter.CreateStartInfo(project, " "));
    }
}
