using SlideDevPresenter.App.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class WebViewNavigationPolicyTests
{
    private readonly WebViewNavigationPolicy _policy = new();

    [Fact]
    public void ShouldOpenExternally_ForRelativeSlideHash_ReturnsFalse()
    {
        var request = new Uri("#/10", UriKind.Relative);

        var result = _policy.ShouldOpenExternally(request, new Uri("http://localhost:3030/"));

        Assert.False(result);
    }

    [Fact]
    public void ShouldOpenExternally_ForLocalSlideHostHash_ReturnsFalse()
    {
        var request = new Uri("http://localhost:3030/#/10");

        var result = _policy.ShouldOpenExternally(request, new Uri("http://localhost:3030/"));

        Assert.False(result);
    }

    [Fact]
    public void ShouldOpenExternally_ForHostedSameOriginSlideHash_ReturnsFalse()
    {
        var current = new Uri("https://slides.example.com/talk");
        var request = new Uri("https://slides.example.com/talk#/10");

        var result = _policy.ShouldOpenExternally(request, current);

        Assert.False(result);
    }

    [Fact]
    public void ShouldOpenExternally_ForInitialHostedLoadWithoutFragment_ReturnsFalse()
    {
        var current = new Uri("https://slides.example.com/talk/de/");
        var request = new Uri("https://slides.example.com/talk/de/");

        var result = _policy.ShouldOpenExternally(request, current);

        Assert.False(result);
    }

    [Fact]
    public void ShouldOpenExternally_ForSameOriginRedirectWithoutFragment_ReturnsFalse()
    {
        var current = new Uri("https://slides.example.com/talk/de");
        var request = new Uri("https://slides.example.com/talk/de/presenter/1");

        var result = _policy.ShouldOpenExternally(request, current);

        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("about:blank")]
    public void ShouldOpenExternally_WhenNoPageLoadedYet_ReturnsFalse(string? current)
    {
        var request = new Uri("https://slides.example.com/talk/");

        var result = _policy.ShouldOpenExternally(request, current is null ? null : new Uri(current));

        Assert.False(result);
    }

    [Fact]
    public void ShouldOpenExternally_ForDifferentLocalPort_ReturnsTrue()
    {
        var request = new Uri("http://localhost:5173/");

        var result = _policy.ShouldOpenExternally(request, new Uri("http://localhost:3030/"));

        Assert.True(result);
    }

    [Fact]
    public void ShouldOpenExternally_ForExternalLink_ReturnsTrue()
    {
        var request = new Uri("https://github.com");

        var result = _policy.ShouldOpenExternally(request, new Uri("http://localhost:3030/"));

        Assert.True(result);
    }

    [Fact]
    public void ShouldOpenExternally_ForNonWebScheme_ReturnsTrue()
    {
        var request = new Uri("mailto:test@example.com");

        var result = _policy.ShouldOpenExternally(request, new Uri("http://localhost:3030/"));

        Assert.True(result);
    }

    [Fact]
    public void ShouldOpenExternally_ForAboutBlankReset_ReturnsFalse()
    {
        var result = _policy.ShouldOpenExternally(new Uri("about:blank"), new Uri("http://localhost:3030/presenter/"));

        Assert.False(result);
    }
}
