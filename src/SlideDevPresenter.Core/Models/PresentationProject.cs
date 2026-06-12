namespace SlideDevPresenter.Core.Models;

public sealed class PresentationProject
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required PresentationSourceType SourceType { get; init; }
    public required string Location { get; set; }
    public string? SlidesFilePath { get; set; }
    public bool IsFavorite { get; set; }
    public DateTimeOffset? LastOpenedAt { get; set; }
}
