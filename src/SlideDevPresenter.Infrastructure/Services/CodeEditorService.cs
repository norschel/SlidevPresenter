using System.Diagnostics;
using System.Runtime.InteropServices;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Infrastructure.Services;

public sealed class CodeEditorService : ICodeEditorService
{
    private readonly OSPlatform _platform;
    private readonly Func<string, string?> _environment;
    private readonly Func<string, bool> _executableExists;
    private readonly Action<ProcessStartInfo> _launch;

    public CodeEditorService()
        : this(
            OperatingSystem.IsWindows() ? OSPlatform.Windows :
            OperatingSystem.IsMacOS() ? OSPlatform.OSX : OSPlatform.Linux,
            Environment.GetEnvironmentVariable,
            IsExecutable,
            startInfo =>
            {
                using var process = Process.Start(startInfo);
                if (process is null)
                    throw new InvalidOperationException("The operating system did not start the editor.");
            })
    {
    }

    internal CodeEditorService(
        OSPlatform platform,
        Func<string, string?> environment,
        Func<string, bool> executableExists,
        Action<ProcessStartInfo> launch)
    {
        _platform = platform;
        _environment = environment;
        _executableExists = executableExists;
        _launch = launch;
    }

    public IReadOnlyList<CodeEditorEdition> GetAvailableEditions() =>
        Enum.GetValues<CodeEditorEdition>().Where(edition => ResolveExecutable(edition) is not null).ToArray();

    public void OpenProject(string projectDirectory, CodeEditorEdition edition)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory) ||
            !Path.IsPathFullyQualified(projectDirectory) ||
            projectDirectory.StartsWith(@"\\", StringComparison.Ordinal) ||
            projectDirectory.StartsWith("//", StringComparison.Ordinal))
        {
            throw new ArgumentException("Choose an absolute local project folder.", nameof(projectDirectory));
        }

        var directory = Path.GetFullPath(projectDirectory);
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"The project folder does not exist: {directory}");

        if (!Enum.IsDefined(edition))
            throw new ArgumentOutOfRangeException(nameof(edition));

        var executable = ResolveExecutable(edition)
            ?? throw new InvalidOperationException(
                $"{DisplayName(edition)} is not installed or could not be found. Install it and try again.");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = directory,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--new-window");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(directory);

        try
        {
            _launch(startInfo);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not open {DisplayName(edition)} for '{directory}': {ex.Message}", ex);
        }
    }

    private string? ResolveExecutable(CodeEditorEdition edition) =>
        Candidates(edition).FirstOrDefault(_executableExists);

    private IEnumerable<string> Candidates(CodeEditorEdition edition)
    {
        var insiders = edition == CodeEditorEdition.Insiders;
        var command = insiders ? "code-insiders" : "code";
        var executable = insiders ? "Code - Insiders.exe" : "Code.exe";
        var windowsDirectory = insiders ? "Microsoft VS Code Insiders" : "Microsoft VS Code";
        var home = _environment(_platform == OSPlatform.Windows ? "USERPROFILE" : "HOME");

        foreach (var entry in (_environment("PATH") ?? "").Split(_platform == OSPlatform.Windows ? ';' : ':'))
        {
            var directory = entry.Trim().Trim('"');
            // Ignore relative PATH entries rather than executing a file from the current project.
            if (!Path.IsPathFullyQualified(directory))
                continue;

            if (_platform == OSPlatform.Windows)
            {
                yield return Path.Combine(directory, executable);
                if (string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)), "bin",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(directory));
                    if (parent is not null)
                        yield return Path.Combine(parent, executable);
                }
            }
            else
            {
                yield return Path.Combine(directory, command);
            }
        }

        if (_platform == OSPlatform.Windows)
        {
            var local = _environment("LOCALAPPDATA");
            if (string.IsNullOrEmpty(local) && !string.IsNullOrEmpty(home))
                local = Path.Combine(home, "AppData", "Local");
            if (!string.IsNullOrEmpty(local))
                yield return Path.Combine(local, "Programs", windowsDirectory, executable);
            foreach (var variable in new[] { "ProgramW6432", "ProgramFiles", "ProgramFiles(x86)" })
            {
                var root = _environment(variable);
                if (!string.IsNullOrEmpty(root))
                    yield return Path.Combine(root, windowsDirectory, executable);
            }
        }
        else if (_platform == OSPlatform.OSX)
        {
            var app = insiders ? "Visual Studio Code - Insiders.app" : "Visual Studio Code.app";
            var roots = string.IsNullOrEmpty(home) ? new[] { "/Applications" } :
                new[] { "/Applications", Path.Combine(home, "Applications") };
            foreach (var root in roots)
            {
                yield return Path.Combine(root, app, "Contents", "Resources", "app", "bin", command);
                yield return Path.Combine(root, app, "Contents", "MacOS", "Electron");
            }
            yield return Path.Combine("/usr/local/bin", command);
            yield return Path.Combine("/opt/homebrew/bin", command);
        }
        else if (_platform == OSPlatform.Linux)
        {
            foreach (var root in new[] { "/usr/local/bin", "/usr/bin", "/bin", "/snap/bin" })
                yield return Path.Combine(root, command);
            yield return Path.Combine("/usr/share", command, "bin", command);
            yield return Path.Combine("/opt", insiders ? "visual-studio-code-insiders" : "visual-studio-code", command);
            if (!string.IsNullOrEmpty(home))
                yield return Path.Combine(home, ".local", "bin", command);
        }
    }

    private static string DisplayName(CodeEditorEdition edition) =>
        edition == CodeEditorEdition.Insiders ? "Visual Studio Code Insiders" : "Visual Studio Code";

    private static bool IsExecutable(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
            return false;
        if (OperatingSystem.IsWindows())
            return string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);

        try
        {
            return (File.GetUnixFileMode(path) &
                (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
