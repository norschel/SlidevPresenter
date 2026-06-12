using System.Text.Json;
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

    public SourceScanner(ILogger<SourceScanner> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<PresentationProject> ScanRoot(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            _logger.LogWarning("Root directory {Path} does not exist, skipping scan.", rootPath);
            return [];
        }

        var results = new List<PresentationProject>();

        // Expand ~ to home directory
        var expandedPath = ExpandHomePath(rootPath);

        try
        {
            // Check the root itself first, then enumerate subdirectories one level deep
            var candidates = new List<string>();

            if (IsSlidevProject(expandedPath))
            {
                candidates.Add(expandedPath);
            }

            foreach (var subDir in Directory.EnumerateDirectories(expandedPath, "*", SearchOption.AllDirectories))
            {
                if (IsSlidevProject(subDir))
                {
                    candidates.Add(subDir);
                }
            }

            foreach (var dir in candidates)
            {
                var slidesFile = FindSlidesFile(dir);
                results.Add(new PresentationProject
                {
                    Id = Guid.NewGuid(),
                    Name = Path.GetFileName(dir),
                    SourceType = PresentationSourceType.LocalProject,
                    Location = dir,
                    SlidesFilePath = slidesFile
                });
            }

            _logger.LogInformation("Scanned {Root}: found {Count} project(s).", rootPath, results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning root {Path}.", rootPath);
        }

        return results;
    }

    public bool IsSlidevProject(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return false;

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

            // Check dependencies and devDependencies for @slidev/cli or slidev
            foreach (var section in new[] { "dependencies", "devDependencies" })
            {
                if (root.TryGetProperty(section, out var deps))
                {
                    foreach (var dep in deps.EnumerateObject())
                    {
                        if (dep.Name.Contains("slidev", StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }

            // Check scripts for slidev commands
            if (root.TryGetProperty("scripts", out var scripts))
            {
                foreach (var script in scripts.EnumerateObject())
                {
                    if (script.Value.GetString()?.Contains("slidev", StringComparison.OrdinalIgnoreCase) == true)
                        return true;
                }
            }
        }
        catch
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

    internal static string ExpandHomePath(string path)
    {
        if (path.StartsWith("~/", StringComparison.Ordinal) || path == "~")
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return path == "~" ? home : Path.Combine(home, path[2..]);
        }
        return path;
    }
}
