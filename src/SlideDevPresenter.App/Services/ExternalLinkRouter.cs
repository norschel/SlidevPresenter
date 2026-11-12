using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.App.Services;

/// <summary>Applies the shared system-browser and embedded-browser link preferences.</summary>
public static class ExternalLinkRouter
{
    public static void Route(
        Uri uri,
        NavigationSettings? settings,
        IExternalBrowserLauncher browserLauncher,
        Action<Uri> openEmbedded)
    {
        if (settings is null || settings.OpenExternalLinksInSystemBrowser)
            browserLauncher.Open(uri);

        if (settings is null || settings.OpenExternalLinksInEmbeddedBrowser)
            openEmbedded(uri);
    }
}
