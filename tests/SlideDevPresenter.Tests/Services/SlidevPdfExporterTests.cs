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
            Location = "/tmp/my talk",
            SlidesFilePath = "/tmp/my talk/slides.md"
        };

        var startInfo = SlidevPdfExporter.CreateStartInfo(project, "/tmp/my output/talk.pdf");

        Assert.Equal("npx", startInfo.FileName);
        Assert.Equal(project.Location, startInfo.WorkingDirectory);
        Assert.Equal(
            ["slidev", "export", project.SlidesFilePath, "--format", "pdf", "--output", "/tmp/my output/talk.pdf"],
            startInfo.ArgumentList);
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
