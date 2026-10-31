using System.Xml.Linq;

namespace SlideDevPresenter.Tests.Views;

public sealed class SettingsWindowLayoutTests
{
    [Theory]
    [InlineData("Defaults", "Default Port")]
    [InlineData("Keyboard Shortcuts", "Restore Defaults")]
    public void ConstrainedSettingsTab_UsesVerticalScrollingForAllControls(string header, string reachableControl)
    {
        XNamespace avalonia = "https://github.com/avaloniaui";
        var viewPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/SlideDevPresenter.App/Views/SettingsWindow.axaml"));
        var document = XDocument.Load(viewPath);
        var window = document.Root!;
        var tab = document
            .Descendants(avalonia + "TabItem")
            .Single(element => (string?)element.Attribute("Header") == header);
        var scrollViewer = tab.Element(avalonia + "ScrollViewer");

        Assert.Equal("600", (string?)window.Attribute("MinWidth"));
        Assert.Equal("420", (string?)window.Attribute("MinHeight"));
        Assert.NotNull(scrollViewer);
        Assert.Contains(
            scrollViewer.Descendants(),
            element => element.Attributes().Any(attribute => attribute.Value == reachableControl));
    }
}
