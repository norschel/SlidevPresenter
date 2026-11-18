using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Infrastructure.Services;

public sealed class SlideDeckBuilder : ISlideDeckBuilder
{
    private static readonly HashSet<string> ExcludedSourceDirectories = new(StringComparer.OrdinalIgnoreCase) { "node_modules", ".git" };
    // Build output and Slidev caches are only excluded at the project root so nested folders such as public/dist still count as sources.
    private static readonly HashSet<string> ExcludedRootDirectories = new(StringComparer.OrdinalIgnoreCase) { "dist", ".slidev" };
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private const int MaxLinkHops = 32;
    private const string BuildMarkerName = ".slidev-presenter-build";
    private static readonly string[] ManifestFiles = ["package.json", "package-lock.json", "npm-shrinkwrap.json", "pnpm-lock.yaml", "yarn.lock", "bun.lock", "bun.lockb", ".npmrc", ".yarnrc.yml"];
    private readonly Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string Output)>> _run;

    public SlideDeckBuilder() : this(RunAsync) { }

    internal SlideDeckBuilder(Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string Output)>> run) => _run = run;

    public async Task<bool> IsBuildRequiredAsync(PresentationProject project, CancellationToken cancellationToken = default)
    {
        EnsureLocalProject(project);
        var directory = Path.GetFullPath(project.Location);
        var dependencyMarker = Path.Combine(directory, "node_modules", ".slidev-presenter-dependencies");
        var buildMarker = GetBuildMarkerPath(directory);
        if (!File.Exists(GetCliPath(directory)) || !File.Exists(dependencyMarker) || !File.Exists(buildMarker)
            || !File.Exists(Path.Combine(directory, "dist", "index.html")))
            return true;
        if (await File.ReadAllTextAsync(dependencyMarker, cancellationToken) != await FingerprintAsync(directory, cancellationToken))
            return true;
        var sources = await Task.Run(() => SourceFingerprint(directory, GetSlidesPath(project, directory), cancellationToken), cancellationToken);
        return await File.ReadAllTextAsync(buildMarker, cancellationToken) != sources;
    }

    public async Task<bool> BuildIfRequiredAsync(PresentationProject project, CancellationToken cancellationToken = default)
    {
        if (!await IsBuildRequiredAsync(project, cancellationToken))
            return false;
        await BuildAsync(project, cancellationToken);
        return true;
    }

    public async Task BuildAsync(PresentationProject project, CancellationToken cancellationToken = default)
    {
        EnsureLocalProject(project);
        var directory = Path.GetFullPath(project.Location);
        var packagePath = Path.Combine(directory, "package.json");
        if (!File.Exists(packagePath))
            throw new InvalidOperationException($"No package.json found in {directory}. Add a Slidev project manifest and install its dependencies manually before compiling.");
        using var package = JsonDocument.Parse(await File.ReadAllTextAsync(packagePath, cancellationToken));
        var manager = SelectPackageManager(directory, package.RootElement);
        var cli = GetCliPath(directory);
        var marker = Path.Combine(directory, "node_modules", ".slidev-presenter-dependencies");
        var fingerprint = await FingerprintAsync(directory, cancellationToken);
        if (!File.Exists(cli) || !File.Exists(marker) || await File.ReadAllTextAsync(marker, cancellationToken) != fingerprint)
        {
            var arguments = manager switch
            {
                "npm" when File.Exists(Path.Combine(directory, "package-lock.json")) || File.Exists(Path.Combine(directory, "npm-shrinkwrap.json")) => new[] { "ci", "--no-audit", "--no-fund" },
                "npm" => ["install", "--no-audit", "--no-fund"],
                "pnpm" or "bun" => ["install", "--frozen-lockfile"],
                "yarn" when File.Exists(Path.Combine(directory, ".yarnrc.yml")) => ["install", "--immutable"],
                _ => new[] { "install", "--frozen-lockfile" }
            };
            // Invalidate both markers first so a failed reinstall can never leave stale markers that skip the next build.
            DeleteIfExists(marker);
            DeleteIfExists(GetBuildMarkerPath(directory));
            await ExecuteAsync(CreateStartInfo(manager, directory, arguments), "Dependency installation", cancellationToken);
            if (!File.Exists(cli))
                throw new InvalidOperationException($"Slidev CLI is missing after dependency installation in {directory}. Add @slidev/cli to the project and install dependencies manually, then retry.");
            await File.WriteAllTextAsync(marker, await FingerprintAsync(directory, cancellationToken), cancellationToken);
        }
        var slides = GetSlidesPath(project, directory);
        var buildMarker = GetBuildMarkerPath(directory);
        // Fingerprint before compiling so edits made during the build still mark the deck as changed.
        var sources = await Task.Run(() => SourceFingerprint(directory, slides, cancellationToken), cancellationToken);
        File.Delete(buildMarker);
        await ExecuteAsync(CreateStartInfo("node", directory, cli, "build", slides), "Slidev compilation", cancellationToken);
        await File.WriteAllTextAsync(buildMarker, sources, cancellationToken);
    }

    private static void EnsureLocalProject(PresentationProject project)
    {
        if (project.SourceType != PresentationSourceType.LocalProject)
            throw new ArgumentException("Only local Slidev projects can be compiled.", nameof(project));
    }

    private static string GetSlidesPath(PresentationProject project, string directory) =>
        Path.GetFullPath(project.SlidesFilePath ?? Path.Combine(directory, "slides.md"));

    private static string GetBuildMarkerPath(string directory) => Path.Combine(directory, "node_modules", BuildMarkerName);

    private static string SourceFingerprint(string directory, string slidesPath, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(directory, slidesPath) + "\n"));
        var files = new List<string>();
        var root = Path.TrimEndingDirectorySeparator(directory);
        var visited = new HashSet<string>(PathComparer) { root };
        var pending = new Stack<(string Path, string Resolved)>([(root, root)]);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (current, resolved) = pending.Pop();
            files.AddRange(Directory.EnumerateFiles(current));
            foreach (var child in Directory.EnumerateDirectories(current))
            {
                var name = Path.GetFileName(child);
                if (ExcludedSourceDirectories.Contains(name) || (current == root && ExcludedRootDirectories.Contains(name)))
                    continue;
                // Directory links may point back to the deck or an ancestor; follow each resolved target only once.
                if (ResolveDirectory(Path.Combine(resolved, name)) is { } target && visited.Add(target))
                    pending.Push((child, target));
            }
        }
        foreach (var file in files.Select(file => Path.GetRelativePath(directory, file)).Order(StringComparer.Ordinal))
        {
            var info = new FileInfo(Path.Combine(directory, file));
            hash.AppendData(Encoding.UTF8.GetBytes($"{file}|{info.Length}|{info.LastWriteTimeUtc.Ticks}\n"));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string? ResolveDirectory(string path)
    {
        for (var hop = 0; hop < MaxLinkHops; hop++)
        {
            var target = new DirectoryInfo(path).LinkTarget;
            if (target is null)
                return path;
            path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target, Path.GetDirectoryName(path)!));
        }
        return null;
    }

    internal static string GetCliPath(string directory) => Path.Combine(directory, "node_modules", "@slidev", "cli", "bin", "slidev.mjs");

    private static string SelectPackageManager(string directory, JsonElement package)
    {
        if (package.TryGetProperty("packageManager", out var configured) && configured.ValueKind == JsonValueKind.String)
        {
            var manager = configured.GetString()!.Split('@')[0];
            if (manager is "npm" or "pnpm" or "yarn" or "bun")
                return manager;
            throw new InvalidOperationException("Unsupported package manager. Use npm, pnpm, Yarn or Bun, or compile the presentation manually.");
        }
        if (File.Exists(Path.Combine(directory, "pnpm-lock.yaml"))) return "pnpm";
        if (File.Exists(Path.Combine(directory, "yarn.lock"))) return "yarn";
        if (File.Exists(Path.Combine(directory, "bun.lock")) || File.Exists(Path.Combine(directory, "bun.lockb"))) return "bun";
        return "npm";
    }

    private static async Task<string> FingerprintAsync(string directory, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var name in ManifestFiles)
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path)) continue;
            hash.AppendData(Encoding.UTF8.GetBytes(name));
            hash.AppendData(await File.ReadAllBytesAsync(path, cancellationToken));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    internal static ProcessStartInfo CreateStartInfo(string executable, string directory, params string[] arguments)
    {
        var info = new ProcessStartInfo(executable)
        {
            WorkingDirectory = directory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true,
            RedirectStandardInput = true, CreateNoWindow = true
        };
        if (OperatingSystem.IsWindows() && executable != "node")
        {
            info.FileName = "powershell.exe";
            info.ArgumentList.Add("-NoProfile");
            info.ArgumentList.Add("-NonInteractive");
            info.ArgumentList.Add("-EncodedCommand");
            var script = "& " + GitCliClient.BuildShellCommand([executable, .. arguments]) + "; exit $LASTEXITCODE";
            info.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        }
        else
        {
            foreach (var argument in arguments)
                info.ArgumentList.Add(argument);
        }
        return info;
    }

    private async Task ExecuteAsync(ProcessStartInfo info, string stage, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _run(info, cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"{stage} failed (exit {result.ExitCode}). {result.Output[^Math.Min(result.Output.Length, 6000)..]}\nFix the project manually in {info.WorkingDirectory}, then retry. Check dependencies, Node.js version and package-registry access.");
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new InvalidOperationException($"{stage} could not start. Install Node.js and the project's package manager, ensure they are on PATH, then restart the presenter. You can also install dependencies and compile manually in {info.WorkingDirectory}.", exception);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(ProcessStartInfo info, CancellationToken cancellationToken)
    {
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start the build process.");
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await Task.WhenAll(output, error);
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Compilation or dependency installation timed out. Run it manually in the project folder, then retry.");
        }
        return (process.ExitCode, (await output) + Environment.NewLine + (await error));
    }
}