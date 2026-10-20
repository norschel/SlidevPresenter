using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class CodeEditorServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Directory.GetCurrentDirectory(), "code-editor-tests-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string?> _environment = new();
    private readonly HashSet<string> _executables = new();
    private readonly List<ProcessStartInfo> _launches = [];

    public CodeEditorServiceTests()
    {
        Directory.CreateDirectory(_root);
        _environment["HOME"] = Path.Combine(_root, "home");
        _environment["USERPROFILE"] = Path.Combine(_root, "home");
    }

    [Fact]
    public void GetAvailableEditions_ReturnsEmptyWhenNothingIsInstalled()
    {
        Assert.Empty(Create(OSPlatform.Linux).GetAvailableEditions());
        Assert.Empty(Create(OSPlatform.Windows).GetAvailableEditions());
        Assert.Empty(Create(OSPlatform.OSX).GetAvailableEditions());
    }

    [Theory]
    [InlineData("linux", "code", "code-insiders")]
    [InlineData("mac", "code", "code-insiders")]
    [InlineData("windows", "Code.exe", "Code - Insiders.exe")]
    public void GetAvailableEditions_DetectsPathEditionsIndependently(
        string platform, string stable, string insiders)
    {
        var bin = Path.Combine(_root, "bin");
        _environment["PATH"] = bin;
        var service = Create(Platform(platform));
        _executables.Add(Path.Combine(bin, insiders));
        Assert.Equal([CodeEditorEdition.Insiders], service.GetAvailableEditions());

        _executables.Add(Path.Combine(bin, stable));
        Assert.Equal([CodeEditorEdition.Stable, CodeEditorEdition.Insiders], service.GetAvailableEditions());

        _executables.Remove(Path.Combine(bin, insiders));
        Assert.Equal([CodeEditorEdition.Stable], service.GetAvailableEditions());
    }

    [Fact]
    public void WindowsPath_ResolvesExecutableBesideBinInsteadOfCmd()
    {
        var install = Path.Combine(_root, "custom VS Code");
        _environment["PATH"] = $"relative;\"{Path.Combine(install, "bin")}\"";
        _executables.Add(Path.Combine(install, "bin", "code.cmd"));
        var service = Create(OSPlatform.Windows);
        Assert.Empty(service.GetAvailableEditions());

        _executables.Add(Path.Combine(install, "Code.exe"));
        service.OpenProject(_root, CodeEditorEdition.Stable);
        Assert.Equal(Path.Combine(install, "Code.exe"), Assert.Single(_launches).FileName);
    }

    [Theory]
    [InlineData("LOCALAPPDATA", "Programs", "Microsoft VS Code", "Code.exe", CodeEditorEdition.Stable)]
    [InlineData("LOCALAPPDATA", "Programs", "Microsoft VS Code Insiders", "Code - Insiders.exe", CodeEditorEdition.Insiders)]
    [InlineData("ProgramFiles", "", "Microsoft VS Code", "Code.exe", CodeEditorEdition.Stable)]
    [InlineData("ProgramFiles(x86)", "", "Microsoft VS Code Insiders", "Code - Insiders.exe", CodeEditorEdition.Insiders)]
    [InlineData("ProgramW6432", "", "Microsoft VS Code", "Code.exe", CodeEditorEdition.Stable)]
    public void Windows_DetectsCommonInstallations(
        string variable, string subdirectory, string product, string executable, CodeEditorEdition edition)
    {
        _environment[variable] = Path.Combine(_root, variable);
        _executables.Add(Path.Combine(_environment[variable]!, subdirectory, product, executable));
        Assert.Equal([edition], Create(OSPlatform.Windows).GetAvailableEditions());
    }

    [Fact]
    public void Windows_DetectsLocalInstallWhenLocalAppDataIsNotSet()
    {
        _executables.Add(Path.Combine(_environment["USERPROFILE"]!, "AppData", "Local",
            "Programs", "Microsoft VS Code", "Code.exe"));
        Assert.Equal([CodeEditorEdition.Stable], Create(OSPlatform.Windows).GetAvailableEditions());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Mac_DetectsBothAppBundlesWithoutPath(bool userInstallation, bool bundledCli)
    {
        var root = userInstallation ? Path.Combine(_environment["HOME"]!, "Applications") : "/Applications";
        foreach (var edition in Enum.GetValues<CodeEditorEdition>())
        {
            var app = edition == CodeEditorEdition.Stable ? "Visual Studio Code.app" : "Visual Studio Code - Insiders.app";
            var command = edition == CodeEditorEdition.Stable ? "code" : "code-insiders";
            _executables.Add(bundledCli
                ? Path.Combine(root, app, "Contents", "Resources", "app", "bin", command)
                : Path.Combine(root, app, "Contents", "MacOS", "Electron"));
        }
        var service = Create(OSPlatform.OSX);
        Assert.Equal([CodeEditorEdition.Stable, CodeEditorEdition.Insiders], service.GetAvailableEditions());
        service.OpenProject(_root, CodeEditorEdition.Insiders);
        Assert.Contains("Visual Studio Code - Insiders.app", Assert.Single(_launches).FileName);
    }

    [Theory]
    [InlineData("/usr/bin/code", CodeEditorEdition.Stable)]
    [InlineData("/usr/local/bin/code-insiders", CodeEditorEdition.Insiders)]
    [InlineData("/snap/bin/code", CodeEditorEdition.Stable)]
    [InlineData("/usr/share/code-insiders/bin/code-insiders", CodeEditorEdition.Insiders)]
    [InlineData("/opt/visual-studio-code/code", CodeEditorEdition.Stable)]
    public void Linux_DetectsCommonInstallations(string executable, CodeEditorEdition edition)
    {
        _executables.Add(executable);
        Assert.Equal([edition], Create(OSPlatform.Linux).GetAvailableEditions());
    }

    [Fact]
    public void Linux_DetectsUserLocalInstallation()
    {
        _executables.Add(Path.Combine(_environment["HOME"]!, ".local", "bin", "code-insiders"));
        Assert.Equal([CodeEditorEdition.Insiders], Create(OSPlatform.Linux).GetAvailableEditions());
    }

    [Fact]
    public void Path_IgnoresEmptyAndRelativeEntriesAndChecksAllDirectories()
    {
        _environment["PATH"] = $":relative:{Path.Combine(_root, "first")}:{Path.Combine(_root, "second")}";
        _executables.UnionWith(["code", Path.Combine("relative", "code"),
            Path.Combine(_root, "second", "code-insiders")]);
        Assert.Equal([CodeEditorEdition.Insiders], Create(OSPlatform.Linux).GetAvailableEditions());
    }

    [Fact]
    public void OpenProject_PreservesSpecialCharactersAsOneArgumentWithoutShell()
    {
        var project = Path.Combine(_root, "-talk with spaces & $(echo injected); 'quote'");
        Directory.CreateDirectory(project);
        _executables.Add("/usr/bin/code");

        Create(OSPlatform.Linux).OpenProject(project, CodeEditorEdition.Stable);

        var info = Assert.Single(_launches);
        Assert.Equal("/usr/bin/code", info.FileName);
        Assert.False(info.UseShellExecute);
        Assert.Equal("", info.Arguments);
        Assert.Equal(project, info.WorkingDirectory);
        Assert.Equal(["--new-window", "--", project], info.ArgumentList);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative/project")]
    [InlineData("https://example.com/project")]
    [InlineData("file:///project")]
    [InlineData(@"\\server\share")]
    [InlineData("//server/share")]
    public void OpenProject_RejectsNonLocalOrNonAbsolutePaths(string? directory)
    {
        Assert.Throws<ArgumentException>(() => Create(OSPlatform.Linux)
            .OpenProject(directory!, CodeEditorEdition.Stable));
        Assert.Empty(_launches);
    }

    [Fact]
    public void OpenProject_RejectsMissingDirectoryAndFiles()
    {
        var file = Path.Combine(_root, "slides.md");
        File.WriteAllText(file, "slides");
        var service = Create(OSPlatform.Linux);
        Assert.Throws<DirectoryNotFoundException>(() => service.OpenProject(file, CodeEditorEdition.Stable));
        Assert.Throws<DirectoryNotFoundException>(() => service.OpenProject(
            Path.Combine(_root, "missing"), CodeEditorEdition.Stable));
        Assert.Empty(_launches);
    }

    [Fact]
    public void OpenProject_RejectsUnknownEdition()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(OSPlatform.Linux)
            .OpenProject(_root, (CodeEditorEdition)42));
    }

    [Fact]
    public void OpenProject_ResolvesAvailabilityAgainAndNeverFallsBackToAnotherEdition()
    {
        _executables.UnionWith(["/usr/bin/code", "/usr/bin/code-insiders"]);
        var service = Create(OSPlatform.Linux);
        Assert.Equal(2, service.GetAvailableEditions().Count);
        _executables.Remove("/usr/bin/code-insiders");

        var error = Assert.Throws<InvalidOperationException>(() =>
            service.OpenProject(_root, CodeEditorEdition.Insiders));

        Assert.Contains("Insiders", error.Message);
        Assert.Contains("not installed", error.Message);
        Assert.Empty(_launches);
    }

    [Fact]
    public void OpenProject_DetectsNewlyInstalledEditor()
    {
        var service = Create(OSPlatform.Linux);
        Assert.Empty(service.GetAvailableEditions());
        _executables.Add("/usr/bin/code");
        service.OpenProject(_root, CodeEditorEdition.Stable);
        Assert.Single(_launches);
    }

    [Fact]
    public void OpenProject_WrapsLaunchFailureWithUsefulContext()
    {
        _executables.Add("/usr/bin/code-insiders");
        var failure = new Win32Exception("Permission denied");
        var service = new CodeEditorService(OSPlatform.Linux, EnvironmentValue, _executables.Contains,
            _ => throw failure);

        var error = Assert.Throws<InvalidOperationException>(() =>
            service.OpenProject(_root, CodeEditorEdition.Insiders));

        Assert.Contains("Insiders", error.Message);
        Assert.Contains(_root, error.Message);
        Assert.Contains("Permission denied", error.Message);
        Assert.Same(failure, error.InnerException);
    }

    private CodeEditorService Create(OSPlatform platform) =>
        new(platform, EnvironmentValue, _executables.Contains, _launches.Add);

    private string? EnvironmentValue(string name) => _environment.GetValueOrDefault(name);

    private static OSPlatform Platform(string name) => name switch
    {
        "windows" => OSPlatform.Windows,
        "mac" => OSPlatform.OSX,
        _ => OSPlatform.Linux
    };

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
