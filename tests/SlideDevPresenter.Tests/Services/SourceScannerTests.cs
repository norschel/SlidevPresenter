using Microsoft.Extensions.Logging.Abstractions;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.Tests.Services;

public class SourceScannerTests : IDisposable
{
    private readonly SourceScanner _scanner = new(NullLogger<SourceScanner>.Instance);
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "SourceScannerTests_" + Guid.NewGuid());

    public SourceScannerTests() => Directory.CreateDirectory(_tempRoot);

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    private string MakeDir(params string[] parts)
    {
        var path = Path.Combine([_tempRoot, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }

    // ── IsSlidevProject tests ─────────────────────────────────────────────

    [Fact]
    public void IsSlidevProject_ReturnsFalse_WhenDirectoryDoesNotExist()
    {
        Assert.False(_scanner.IsSlidevProject(Path.Combine(_tempRoot, "nonexistent")));
    }

    [Fact]
    public void IsSlidevProject_ReturnsTrue_WhenSlidesMdPresent()
    {
        var dir = MakeDir("has-slides-md");
        File.WriteAllText(Path.Combine(dir, "slides.md"), "# Hello");
        Assert.True(_scanner.IsSlidevProject(dir));
    }

    [Fact]
    public void IsSlidevProject_ReturnsTrue_WhenDotSlidevFolderPresent()
    {
        var dir = MakeDir("has-dot-slidev");
        Directory.CreateDirectory(Path.Combine(dir, ".slidev"));
        Assert.True(_scanner.IsSlidevProject(dir));
    }

    [Fact]
    public void IsSlidevProject_ReturnsTrue_WhenPackageJsonReferencesSlidev()
    {
        var dir = MakeDir("has-package-json");
        File.WriteAllText(Path.Combine(dir, "package.json"), """
            {
              "devDependencies": {
                "@slidev/cli": "^0.49.0"
              }
            }
            """);
        Assert.True(_scanner.IsSlidevProject(dir));
    }

    [Fact]
    public void IsSlidevProject_ReturnsTrue_WhenPackageJsonHasSlidevScript()
    {
        var dir = MakeDir("has-slidev-script");
        File.WriteAllText(Path.Combine(dir, "package.json"), """
            {
              "scripts": {
                "dev": "slidev --open"
              }
            }
            """);
        Assert.True(_scanner.IsSlidevProject(dir));
    }

    [Fact]
    public void IsSlidevProject_ReturnsTrue_WhenCompanionFoldersAndMdPresent()
    {
        var dir = MakeDir("has-companion");
        Directory.CreateDirectory(Path.Combine(dir, "components"));
        File.WriteAllText(Path.Combine(dir, "deck.md"), "# Deck");
        Assert.True(_scanner.IsSlidevProject(dir));
    }

    [Fact]
    public void IsSlidevProject_ReturnsFalse_WhenFolderIsEmpty()
    {
        var dir = MakeDir("empty");
        Assert.False(_scanner.IsSlidevProject(dir));
    }

    [Fact]
    public void IsSlidevProject_ReturnsFalse_WhenPackageJsonHasNoSlidevReference()
    {
        var dir = MakeDir("unrelated-package");
        File.WriteAllText(Path.Combine(dir, "package.json"), """
            {
              "dependencies": {
                "react": "^18.0.0"
              }
            }
            """);
        Assert.False(_scanner.IsSlidevProject(dir));
    }

    // ── ScanRoot tests ─────────────────────────────────────────────────────

    [Fact]
    public void ScanRoot_ReturnsEmpty_WhenRootDoesNotExist()
    {
        var result = _scanner.ScanRoot(Path.Combine(_tempRoot, "ghost"));
        Assert.Empty(result);
    }

    [Fact]
    public void ScanRoot_FindsDirectChildProjects()
    {
        var projectDir = MakeDir("root", "my-talk");
        File.WriteAllText(Path.Combine(projectDir, "slides.md"), "# Talk");

        var results = _scanner.ScanRoot(Path.Combine(_tempRoot, "root"));
        Assert.Single(results);
        Assert.Equal(projectDir, results[0].Location);
        Assert.Equal(PresentationSourceType.LocalProject, results[0].SourceType);
    }

    [Fact]
    public void ScanRoot_FindsNestedProjects()
    {
        var nested = MakeDir("deep-root", "talks", "intro");
        File.WriteAllText(Path.Combine(nested, "slides.md"), "# Intro");

        var results = _scanner.ScanRoot(Path.Combine(_tempRoot, "deep-root"));
        Assert.Single(results);
        Assert.Equal(nested, results[0].Location);
    }

    [Fact]
    public void ScanRoot_SetsProjectNameToDirectoryName()
    {
        var dir = MakeDir("root2", "github-actions-intro");
        File.WriteAllText(Path.Combine(dir, "slides.md"), "# GH Actions");

        var results = _scanner.ScanRoot(Path.Combine(_tempRoot, "root2"));
        Assert.Equal("github-actions-intro", results[0].Name);
    }

    [Fact]
    public void ScanRoot_SetsSlidesFilePath_WhenSlidesMdPresent()
    {
        var dir = MakeDir("root3", "my-pres");
        var slidesPath = Path.Combine(dir, "slides.md");
        File.WriteAllText(slidesPath, "# Pres");

        var results = _scanner.ScanRoot(Path.Combine(_tempRoot, "root3"));
        Assert.Equal(slidesPath, results[0].SlidesFilePath);
    }

    // ── ExpandHomePath tests ──────────────────────────────────────────────

    [Fact]
    public void ExpandHomePath_ExpandsTildeSlash()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var result = SourceScanner.ExpandHomePath("~/talks");
        Assert.Equal(Path.Combine(home, "talks"), result);
    }

    [Fact]
    public void ExpandHomePath_ReturnsSamePathForAbsolutePath()
    {
        // Use a rooted path that is valid on all platforms
        var absolute = Path.GetFullPath("presentations");
        Assert.Equal(absolute, SourceScanner.ExpandHomePath(absolute));
    }
}
