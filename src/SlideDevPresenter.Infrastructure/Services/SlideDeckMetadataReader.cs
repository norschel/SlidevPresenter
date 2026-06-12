using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Infrastructure.Services;

public sealed class SlideDeckMetadataReader(ILogger<SlideDeckMetadataReader> logger) : ISlideDeckMetadataReader
{
    private static readonly Regex SlideDelimiterRegex = new(@"^---\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex HeadingRegex = new(@"^#{1,6}\s+(.*)$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex HtmlCommentRegex = new(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);

    public async Task<SlideDeckMetadata> ReadAsync(PresentationProject project, CancellationToken cancellationToken = default)
    {
        var slidesPath = ResolveSlidesPath(project);
        if (slidesPath is null || !File.Exists(slidesPath))
            return SlideDeckMetadata.Empty(project.Name);

        try
        {
            var content = await File.ReadAllTextAsync(slidesPath, cancellationToken);
            return Parse(project.Name, content);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read slide deck metadata for {Project}.", project.Name);
            return SlideDeckMetadata.Empty(project.Name);
        }
    }

    internal static SlideDeckMetadata Parse(string fallbackTitle, string content)
    {
        var normalized = content.Replace("\r\n", "\n");
        normalized = StripLeadingFrontMatter(normalized);

        var sections = SlideDelimiterRegex.Split(normalized)
            .Select(section => section.Trim())
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .ToList();

        if (sections.Count == 0)
            return SlideDeckMetadata.Empty(fallbackTitle);

        var slides = new List<SlideDeckSlide>(sections.Count);
        for (var index = 0; index < sections.Count; index++)
        {
            var section = sections[index];
            var commentFreeSection = HtmlCommentRegex.Replace(section, string.Empty);
            var heading = HeadingRegex.Match(commentFreeSection);
            var title = heading.Success
                ? heading.Groups[1].Value.Trim()
                : $"Slide {index + 1}";

            var summary = commentFreeSection.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(line => !line.StartsWith('#'))
                ?? title;

            slides.Add(new SlideDeckSlide(index + 1, title, summary));
        }

        return new SlideDeckMetadata
        {
            DeckTitle = slides.Count > 0 && slides[0].Title.Length > 0 ? slides[0].Title : fallbackTitle,
            Slides = slides
        };
    }

    private static string StripLeadingFrontMatter(string content)
    {
        if (!content.StartsWith("---\n", StringComparison.Ordinal))
            return content;

        var secondDelimiter = content.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        return secondDelimiter >= 0
            ? content[(secondDelimiter + 5)..]
            : content;
    }

    private static string? ResolveSlidesPath(PresentationProject project)
    {
        if (!string.IsNullOrWhiteSpace(project.SlidesFilePath))
            return project.SlidesFilePath;

        return project.SourceType == PresentationSourceType.HostedUrl
            ? null
            : Path.Combine(project.Location, "slides.md");
    }
}
