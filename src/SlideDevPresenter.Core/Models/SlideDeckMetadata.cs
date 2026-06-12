namespace SlideDevPresenter.Core.Models;

public sealed class SlideDeckMetadata
{
    public string DeckTitle { get; init; } = string.Empty;
    public IReadOnlyList<SlideDeckSlide> Slides { get; init; } = [];

    public static SlideDeckMetadata Empty(string deckTitle) => new()
    {
        DeckTitle = deckTitle,
        Slides = []
    };
}
