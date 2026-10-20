using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.Tests.Services;

public class SlidevPdfExporterTests
{
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
