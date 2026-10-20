using System.Diagnostics;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class SlideDeckBuilderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "presenter-build-" + Guid.NewGuid());
    private readonly List<ProcessStartInfo> _commands = [];
    private readonly SlideDeckBuilder _builder;
    private int _exitCode;

    public SlideDeckBuilderTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "package.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "slides.md"), "# Talk");
        _builder = new SlideDeckBuilder((info, token) =>
        {
            token.ThrowIfCancellationRequested();
            _commands.Add(info);
            if (_exitCode == 0 && info.FileName != "node")
            {
                var cli = SlideDeckBuilder.GetCliPath(_directory);
                Directory.CreateDirectory(Path.GetDirectoryName(cli)!);
                File.WriteAllText(cli, "");
            }
            return Task.FromResult((_exitCode, _exitCode == 0 ? "" : "compiler failure details"));
        });
    }

    public void Dispose() => Directory.Delete(_directory, true);

    [Theory]
    [InlineData("package-lock.json", "npm", "ci")]
    [InlineData("pnpm-lock.yaml", "pnpm", "--frozen-lockfile")]
    [InlineData("yarn.lock", "yarn", "--frozen-lockfile")]
    [InlineData("bun.lock", "bun", "--frozen-lockfile")]
    public async Task BuildAsync_InstallsUsingProjectLockfileBeforeCompiling(string lockfile, string manager, string installArgument)
    {
        File.WriteAllText(Path.Combine(_directory, lockfile), "lock contents");

        await _builder.BuildAsync(Project());

        Assert.Equal(2, _commands.Count);
        var install = CommandText(_commands[0]);
        Assert.Contains(manager, install);
        Assert.Contains(installArgument, install);
        var build = _commands[1];
        Assert.Equal("node", build.FileName);
        Assert.Equal(_directory, build.WorkingDirectory);
        Assert.Equal(new[] { SlideDeckBuilder.GetCliPath(_directory), "build", Path.Combine(_directory, "slides.md") }, build.ArgumentList);
    }

    [Fact]
    public async Task BuildAsync_UnchangedDependencies_ReusesInstallButRecompilesSlides()
    {
        await _builder.BuildAsync(Project());
        _commands.Clear();

        await _builder.BuildAsync(Project());

        Assert.Equal("node", Assert.Single(_commands).FileName);
    }

    [Fact]
    public async Task BuildAsync_ManifestChangedAfterBranchSwitch_ReinstallsDependencies()
    {
        await _builder.BuildAsync(Project());
        _commands.Clear();
        File.WriteAllText(Path.Combine(_directory, "package.json"), "{\"name\":\"new-branch\"}");

        await _builder.BuildAsync(Project());

        Assert.Equal(2, _commands.Count);
    }

    [Fact]
    public async Task BuildAsync_InstallFails_ShowsManualRecoveryAndDoesNotCompile()
    {
        _exitCode = 1;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _builder.BuildAsync(Project()));

        Assert.Single(_commands);
        Assert.Contains("Dependency installation failed", error.Message);
        Assert.Contains("compiler failure details", error.Message);
        Assert.Contains("Fix the project manually", error.Message);
    }

    [Fact]
    public async Task BuildAsync_CompilerFails_ReportsFailure()
    {
        await _builder.BuildAsync(Project());
        _exitCode = 1;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _builder.BuildAsync(Project()));
        Assert.Contains("Slidev compilation failed", error.Message);
    }

    [Fact]
    public async Task BuildAsync_Cancelled_DoesNotStartProcess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _builder.BuildAsync(Project(), cancellation.Token));
        Assert.Empty(_commands);
    }

    private PresentationProject Project() => new()
    {
        Id = Guid.NewGuid(), Name = "Talk", SourceType = PresentationSourceType.LocalProject,
        Location = _directory, SlidesFilePath = Path.Combine(_directory, "slides.md")
    };

    private static string CommandText(ProcessStartInfo info) => info.FileName == "powershell.exe"
        ? System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(info.ArgumentList[^1]))
        : info.FileName + " " + string.Join(' ', info.ArgumentList);
}