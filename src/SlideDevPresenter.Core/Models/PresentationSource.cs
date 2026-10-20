namespace SlideDevPresenter.Core.Models;

public sealed class PresentationSource
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required PresentationSourceType Type { get; init; }
    public required string Location { get; set; }
    public string? CheckoutPath { get; set; }
    public string? Branch { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string Comment { get; set; } = string.Empty;
    public bool IsPublic { get; set; } = true;
    public bool RequiresAuthentication { get; set; }
}
