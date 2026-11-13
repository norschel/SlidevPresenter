using System.Xml.Linq;

namespace SlideDevPresenter.Tests.Views;

public sealed class MainWindowLayoutTests
{
    private static XElement PresentationToolbar()
    {
        XNamespace avalonia = "https://github.com/avaloniaui";
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/SlideDevPresenter.App/MainWindow.axaml"));
        return XDocument.Load(path).Descendants(avalonia + "StackPanel")
            .Single(element => (string?)element.Attribute("IsVisible") == "{Binding IsPresentationRibbonSelected}");
    }

    [Fact]
    public void PresentationToolbar_NavigationPrecedesStartAndIsEnabledOnlyWhileRunning()
    {
        var buttons = PresentationToolbar().Elements().ToArray();

        Assert.Equal("Previous", (string?)buttons[0].Attribute("Content"));
        Assert.Equal("Previous_Click", (string?)buttons[0].Attribute("Click"));
        Assert.Equal("Next", (string?)buttons[1].Attribute("Content"));
        Assert.Equal("Next_Click", (string?)buttons[1].Attribute("Click"));
        Assert.Equal("▶ Start", (string?)buttons[2].Attribute("Content"));
        Assert.All(buttons.Take(2), button =>
            Assert.Equal("{Binding IsRunning}", (string?)button.Attribute("IsEnabled")));
    }

    [Theory]
    [InlineData("StartFromBeginningButton", "StartFromBeginningCommand")]
    [InlineData("StartFromCurrentButton", "StartFromCurrentSlideCommand")]
    [InlineData("StartPresenterViewButton", "StartPresenterViewCommand")]
    public void PresentationToolbar_StartButtonsAreGatedWithoutChangingNavigationCommands(string name, string command)
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var button = PresentationToolbar().Elements()
            .Single(element => (string?)element.Attribute(xaml + "Name") == name);

        Assert.Equal("{Binding CanStartSelectedPresentation}", (string?)button.Attribute("IsEnabled"));
        Assert.Equal($"{{Binding {command}}}", (string?)button.Attribute("Command"));
    }
}
