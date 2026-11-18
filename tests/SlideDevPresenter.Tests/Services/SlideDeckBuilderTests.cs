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
            if (_exitCode == 0 && info.FileName == "node")
            {
                Directory.CreateDirectory(Path.Combine(_directory, "dist"));
                File.WriteAllText(Path.Combine(_directory, "dist", "index.html"), "<html></html>");
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
    public async Task BuildIfRequiredAsync_NoChanges_SkipsCompilation()
    {
        Assert.True(await _builder.IsBuildRequiredAsync(Project()));
        Assert.True(await _builder.BuildIfRequiredAsync(Project()));
        _commands.Clear();

        Assert.False(await _builder.IsBuildRequiredAsync(Project()));
        Assert.False(await _builder.BuildIfRequiredAsync(Project()));
        Assert.Empty(_commands);
    }

    [Fact]
    public async Task BuildIfRequiredAsync_SlidesEdited_Recompiles()
    {
        await _builder.BuildAsync(Project());
        _commands.Clear();
        File.WriteAllText(Path.Combine(_directory, "slides.md"), "# Talk\n\n---\n\n# New slide");

        Assert.True(await _builder.BuildIfRequiredAsync(Project()));

        Assert.Equal("node", Assert.Single(_commands).FileName);
    }

    [Fact]
    public async Task IsBuildRequiredAsync_IgnoresOutputAndDependencyFolders()
    {
        await _builder.BuildAsync(Project());
        File.WriteAllText(Path.Combine(_directory, "dist", "extra.js"), "generated");
        File.WriteAllText(Path.Combine(_directory, "node_modules", "cache.txt"), "cache");

        Assert.False(await _builder.IsBuildRequiredAsync(Project()));
    }

    [Fact]
    public async Task IsBuildRequiredAsync_NestedDistFolder_CountsAsSource()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "public", "dist"));
        File.WriteAllText(Path.Combine(_directory, "public", "dist", "logo.svg"), "<svg/>");
        await _builder.BuildAsync(Project());

        File.WriteAllText(Path.Combine(_directory, "public", "dist", "logo.svg"), "<svg width=\"10\"/>");

        Assert.True(await _builder.IsBuildRequiredAsync(Project()));
    }

    [Fact]
    public async Task IsBuildRequiredAsync_DirectorySymlinkCycle_DoesNotLoop()
    {
        if (!TryCreateDirectoryLink(Path.Combine(_directory, "loop"), _directory))
            return;

        await _builder.BuildAsync(Project());

        Assert.False(await _builder.IsBuildRequiredAsync(Project()));
    }

    [Fact]
    public async Task IsBuildRequiredAsync_LinkedSourceEdited_RequiresBuild()
    {
        var shared = Path.Combine(Path.GetTempPath(), "presenter-shared-" + Guid.NewGuid());
        Directory.CreateDirectory(shared);
        try
        {
            File.WriteAllText(Path.Combine(shared, "theme.css"), "a {}");
            if (!TryCreateDirectoryLink(Path.Combine(_directory, "shared"), shared))
                return;
            await _builder.BuildAsync(Project());

            File.WriteAllText(Path.Combine(shared, "theme.css"), "a { color: red; }");

            Assert.True(await _builder.IsBuildRequiredAsync(Project()));
        }
        finally
        {
            Directory.Delete(shared, true);
        }
    }

    [Fact]
    public async Task IsBuildRequiredAsync_LinkRetargetedToVisitedDirectory_RequiresBuild()
    {
        var light = Path.Combine(_directory, "light");
        var dark = Path.Combine(_directory, "dark");
        Directory.CreateDirectory(light);
        Directory.CreateDirectory(dark);
        File.WriteAllText(Path.Combine(light, "theme.css"), "light {}");
        File.WriteAllText(Path.Combine(dark, "theme.css"), "dark {}");
        var theme = Path.Combine(_directory, "theme");
        if (!TryCreateDirectoryLink(theme, "light"))
            return;

        await _builder.BuildAsync(Project());
        Directory.Delete(theme);
        Directory.CreateSymbolicLink(theme, "dark");

        Assert.True(await _builder.IsBuildRequiredAsync(Project()));
    }

    [Fact]
    public async Task IsBuildRequiredAsync_LinkTargetWithIntermediateSymlinkAndParentSegment_RequiresBuild()
    {
        var external = Path.Combine(Path.GetTempPath(), "presenter-external-" + Guid.NewGuid());
        var common = Path.Combine(external, "common");
        var externalAssets = Path.Combine(external, "assets");
        Directory.CreateDirectory(common);
        Directory.CreateDirectory(externalAssets);
        Directory.CreateDirectory(Path.Combine(_directory, "assets"));
        File.WriteAllText(Path.Combine(externalAssets, "theme.css"), "a {}");
        File.WriteAllText(Path.Combine(_directory, "assets", "theme.css"), "b {}");
        try
        {
            if (!TryCreateDirectoryLink(Path.Combine(_directory, "shared"), common)
                || !TryCreateDirectoryLink(Path.Combine(_directory, "theme"), "shared/../assets"))
                return;
            await _builder.BuildAsync(Project());

            File.WriteAllText(Path.Combine(externalAssets, "theme.css"), "a { color: red; }");

            Assert.True(await _builder.IsBuildRequiredAsync(Project()));
        }
        finally
        {
            Directory.Delete(external, true);
        }
    }

    [Fact]
    public async Task IsBuildRequiredAsync_SymlinkedProjectRoot_DoesNotRevisitExcludedOutput()
    {
        var alias = Path.Combine(Path.GetTempPath(), "presenter-root-" + Guid.NewGuid());
        if (!TryCreateDirectoryLink(alias, _directory))
            return;
        try
        {
            if (!TryCreateDirectoryLink(Path.Combine(_directory, "loop"), _directory))
                return;
            Directory.CreateDirectory(Path.Combine(_directory, "dist"));
            File.WriteAllText(Path.Combine(_directory, "dist", "generated.js"), "first");
            var project = new PresentationProject
            {
                Id = Guid.NewGuid(), Name = "Talk", SourceType = PresentationSourceType.LocalProject,
                Location = alias, SlidesFilePath = Path.Combine(alias, "slides.md")
            };
            await _builder.BuildAsync(project);

            File.WriteAllText(Path.Combine(_directory, "dist", "generated.js"), "changed");

            Assert.False(await _builder.IsBuildRequiredAsync(project));
        }
        finally
        {
            Directory.Delete(alias);
        }
    }

    [Fact]
    public async Task BuildAsync_ReinstallFails_InvalidatesMarkers()
    {
        await _builder.BuildAsync(Project());
        var cli = SlideDeckBuilder.GetCliPath(_directory);
        File.Delete(cli);
        _exitCode = 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _builder.BuildAsync(Project()));
        // The failed installer may still have restored the CLI.
        File.WriteAllText(cli, "");
        _exitCode = 0;

        Assert.True(await _builder.IsBuildRequiredAsync(Project()));
    }

    [Fact]
    public async Task IsBuildRequiredAsync_OutputMissing_RequiresBuild()
    {
        await _builder.BuildAsync(Project());
        Directory.Delete(Path.Combine(_directory, "dist"), true);

        Assert.True(await _builder.IsBuildRequiredAsync(Project()));
    }

    [Fact]
    public async Task IsBuildRequiredAsync_AfterFailedBuild_RequiresBuild()
    {
        await _builder.BuildAsync(Project());
        _exitCode = 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _builder.BuildAsync(Project()));
        _exitCode = 0;

        Assert.True(await _builder.IsBuildRequiredAsync(Project()));
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

    // Windows only allows creating symbolic links with Developer Mode or elevation.
    private static bool TryCreateDirectoryLink(string path, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(path, target);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string CommandText(ProcessStartInfo info) => info.FileName == "powershell.exe"
        ? System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(info.ArgumentList[^1]))
        : info.FileName + " " + string.Join(' ', info.ArgumentList);
}