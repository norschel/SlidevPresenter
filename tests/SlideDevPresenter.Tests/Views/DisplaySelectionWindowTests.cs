using System.Xml.Linq;

namespace SlideDevPresenter.Tests.Views;

public sealed class DisplaySelectionWindowTests
{
    [Fact]
    public void DisplayGuidance_IdentifiesParticipantViewWithoutRelyingOnMonitorNamesOrCount()
    {
        XNamespace avalonia = "https://github.com/avaloniaui";
        var viewPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/SlideDevPresenter.App/Views/DisplaySelectionWindow.axaml"));
        var document = XDocument.Load(viewPath);
        var guidance = document
            .Descendants(avalonia + "TextBlock")
            .Select(element => (string?)element.Attribute("Text"))
            .Single(text => text?.Contains("participant view", StringComparison.OrdinalIgnoreCase) == true);

        Assert.Equal("Choose the display where the participant view should appear.", guidance);
        Assert.Contains(
            document.Descendants(avalonia + "TextBlock"),
            element => (string?)element.Attribute("Text") == "{Binding DisplayLabel}");
    }
}
