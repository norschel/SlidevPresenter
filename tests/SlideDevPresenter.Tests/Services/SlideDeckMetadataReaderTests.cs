using Microsoft.Extensions.Logging.Abstractions;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class SlideDeckMetadataReaderTests
{
    [Fact]
    public void Parse_ExtractsDeckTitleAndSlideSummaries()
    {
        const string markdown = """
---
theme: default
---
# Kickoff
Intro line
---
## Agenda
- One
- Two
---
Plain body slide
More detail
""";

        var metadata = SlideDeckMetadataReader.Parse("Fallback", markdown);

        Assert.Equal("Kickoff", metadata.DeckTitle);
        Assert.Collection(
            metadata.Slides,
            slide =>
            {
                Assert.Equal(1, slide.Number);
                Assert.Equal("Kickoff", slide.Title);
                Assert.Equal("Intro line", slide.Summary);
            },
            slide =>
            {
                Assert.Equal(2, slide.Number);
                Assert.Equal("Agenda", slide.Title);
                Assert.Equal("- One", slide.Summary);
            },
            slide =>
            {
                Assert.Equal(3, slide.Number);
                Assert.Equal("Slide 3", slide.Title);
                Assert.Equal("Plain body slide", slide.Summary);
            });
    }

    [Fact]
    public async Task ReadAsync_ReturnsEmptyMetadataForHostedUrl()
    {
        var reader = new SlideDeckMetadataReader(NullLogger<SlideDeckMetadataReader>.Instance);
        var project = new PresentationProject
        {
            Id = Guid.NewGuid(),
            Name = "Remote Talk",
            SourceType = PresentationSourceType.HostedUrl,
            Location = "https://slides.example.com"
        };

        var metadata = await reader.ReadAsync(project);

        Assert.Equal("Remote Talk", metadata.DeckTitle);
        Assert.Empty(metadata.Slides);
    }
}
