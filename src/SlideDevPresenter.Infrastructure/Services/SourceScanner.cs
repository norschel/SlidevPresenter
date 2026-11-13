using System.Text.Json;
using System.Security;
using Microsoft.Extensions.Logging;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Infrastructure.Services;

/// <summary>
/// Scans local directories for Slidev projects.
/// A folder is considered a Slidev project if it contains any of:
///   - slides.md
///   - package.json with a Slidev dependency or script
///   - .slidev folder
///   - components, layouts, or public folder alongside a slides.md entry file
/// </summary>
public sealed class SourceScanner : ISourceScanner
{
    private readonly ILogger<SourceScanner> _logger;
    private readonly Func<string, IEnumerable<string>> _enumerateDirectories;
    private readonly Func<string, FileAttributes> _getAttributes;

    public SourceScanner(ILogger<SourceScanner> logger)
        : this(logger, Directory.EnumerateDirectories, File.GetAttributes) { }

    internal SourceScanner(
        ILogger<SourceScanner> logger,
        Func<string, IEnumerable<string>> enumerateDirectories,
        Func<string, FileAttributes> getAttributes)
    {
        _logger = logger;
        _enumerateDirectories = enumerateDirectories;
        _getAttributes = getAttributes;
    }

    public IReadOnlyList<PresentationProject> ScanRoot(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            _logger.LogWarning("Root directory path is empty, skipping scan.");
            return [];
        }

        // Expand ~ to home directory before validating existence.
        var expandedPath = ExpandHomePath(rootPath.Trim());

        if (!Directory.Exists(expandedPath))
        {
            _logger.LogWarning("Root directory {Path} does not exist, skipping scan.", expandedPath);
            return [];
        }

        var results = new List<PresentationProject>();

        var candidates = new List<string>();
        ScanCandidate(expandedPath, candidates);

        foreach (var subDir in EnumerateProjectDirectories(
                     expandedPath,
                     (path, exception) => _logger.LogWarning(exception, "Could not scan child directory {Path}; continuing with other directories.", path),
                     _enumerateDirectories,
                     _getAttributes))
        {
            ScanCandidate(subDir, candidates);
        }

        foreach (var dir in candidates)
        {
            try
            {
                results.Add(new PresentationProject
                {
                    Id = Guid.NewGuid(),
                    Name = Path.GetFileName(dir),
                    SourceType = PresentationSourceType.LocalProject,
                    Location = dir,
                    SlidesFilePath = FindSlidesFile(dir)
                });
            }
            catch (Exception exception) when (IsExpectedScanFailure(exception))
            {
                _logger.LogWarning(exception, "Could not read project directory {Path}; continuing with other projects.", dir);
            }
        }

        _logger.LogInformation("Scanned {Root}: found {Count} project(s).", expandedPath, results.Count);
        return results;

        void ScanCandidate(string path, List<string> found)
        {
            try
            {
                if (IsSlidevProject(path))
                    found.Add(path);
            }
            catch (Exception exception) when (IsExpectedScanFailure(exception))
            {
                _logger.LogWarning(exception, "Could not inspect project directory {Path}; continuing with other projects.", path);
            }
        }
    }

    internal static IEnumerable<string> EnumerateProjectDirectories(
        string root,
        Action<string, Exception> onError,
        Func<string, IEnumerable<string>>? enumerateDirectories = null,
        Func<string, FileAttributes>? getAttributes = null)
    {
        enumerateDirectories ??= Directory.EnumerateDirectories;
        getAttributes ??= File.GetAttributes;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var current))
        {
            string[] children;
            try
            {
                children = enumerateDirectories(current).ToArray();
            }
            catch (Exception exception) when (IsExpectedScanFailure(exception))
            {
                onError(current, exception);
                continue;
            }

            foreach (var directory in children)
            {
                if (Path.GetFileName(directory) is "node_modules" or ".git" or "dist" or ".yarn" or ".slidev")
                    continue;

                FileAttributes attributes;
                try
                {
                    attributes = getAttributes(directory);
                }
                catch (Exception exception) when (IsExpectedScanFailure(exception))
                {
                    onError(directory, exception);
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
                yield return directory;
                pending.Push(directory);
            }
        }
    }

    private static bool IsExpectedScanFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException;

    public bool IsSlidevProject(string directoryPath)
    {
        var expandedPath = ExpandHomePath(directoryPath);
        if (!Directory.Exists(expandedPath))
            return false;
        directoryPath = expandedPath;

        // 1. slides.md exists
        if (File.Exists(Path.Combine(directoryPath, "slides.md")))
            return true;

        // 2. .slidev folder exists
        if (Directory.Exists(Path.Combine(directoryPath, ".slidev")))
            return true;

        // 3. package.json references slidev
        var packageJsonPath = Path.Combine(directoryPath, "package.json");
        if (File.Exists(packageJsonPath) && PackageJsonReferencesSlidev(packageJsonPath))
            return true;

        // 4. Slidev companion folders present (components/layouts/public) plus any .md entry
        if (HasSlidevCompanionFolders(directoryPath) && HasMarkdownEntry(directoryPath))
            return true;

        return false;
    }

    private static string? FindSlidesFile(string directoryPath)
    {
        var slidesPath = Path.Combine(directoryPath, "slides.md");
        if (File.Exists(slidesPath))
            return slidesPath;

        // Fallback: first .md file in the directory
        return Directory.EnumerateFiles(directoryPath, "*.md", SearchOption.TopDirectoryOnly).FirstOrDefault();
    }

    private static bool PackageJsonReferencesSlidev(string packageJsonPath)
    {
        try
        {
            var content = File.ReadAllText(packageJsonPath);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            // Check dependencies and devDependencies for @slidev/cli or slidev
            foreach (var section in new[] { "dependencies", "devDependencies" })
            {
                if (root.TryGetProperty(section, out var deps) && deps.ValueKind == JsonValueKind.Object)
                {
                    foreach (var dep in deps.EnumerateObject())
                    {
                        if (dep.Name.Contains("slidev", StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }

            // Check scripts for slidev commands
            if (root.TryGetProperty("scripts", out var scripts) && scripts.ValueKind == JsonValueKind.Object)
            {
                foreach (var script in scripts.EnumerateObject())
                {
                    if (script.Value.ValueKind == JsonValueKind.String
                        && script.Value.GetString()?.Contains("slidev", StringComparison.OrdinalIgnoreCase) == true)
                        return true;
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or SecurityException)
        {
            // Malformed JSON or IO error — not a match
        }

        return false;
    }

    private static bool HasSlidevCompanionFolders(string directoryPath)
    {
        return Directory.Exists(Path.Combine(directoryPath, "components"))
            || Directory.Exists(Path.Combine(directoryPath, "layouts"))
            || Directory.Exists(Path.Combine(directoryPath, "public"));
    }

    private static bool HasMarkdownEntry(string directoryPath)
    {
        return Directory.EnumerateFiles(directoryPath, "*.md", SearchOption.TopDirectoryOnly).Any();
    }

    public static string ExpandHomePath(string path)
    {
        if (path.StartsWith("~/", StringComparison.Ordinal) || path == "~")
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return path == "~" ? home : Path.Combine(home, path[2..]);
        }
        return path;
    }
}
