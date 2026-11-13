using SlideDevPresenter.App.Services;
using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Tests.Services;

public sealed class ExternalLinkRouterTests
{
    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, false, false)]
    public void Route_RespectsBothConfiguredDestinations(
        bool systemPreference,
        bool embeddedPreference,
        bool expectSystem,
        bool expectEmbedded)
    {
        var uri = new Uri("https://example.com/slides");
        var launcher = new RecordingBrowserLauncher();
        Uri? embeddedUri = null;
        var settings = new NavigationSettings
        {
            OpenExternalLinksInSystemBrowser = systemPreference,
            OpenExternalLinksInEmbeddedBrowser = embeddedPreference
        };

        ExternalLinkRouter.Route(uri, settings, launcher, value => embeddedUri = value);

        Assert.Equal(expectSystem, launcher.OpenedUri is not null);
        Assert.Equal(expectEmbedded, embeddedUri is not null);
        Assert.Equal(expectSystem ? uri : null, launcher.OpenedUri);
        Assert.Equal(expectEmbedded ? uri : null, embeddedUri);
    }

    private sealed class RecordingBrowserLauncher : IExternalBrowserLauncher
    {
        public Uri? OpenedUri { get; private set; }
        public void Open(Uri uri) => OpenedUri = uri;
    }
}
