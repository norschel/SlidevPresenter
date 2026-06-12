using System.Diagnostics;

namespace SlideDevPresenter.App.Services;

public interface IExternalBrowserLauncher
{
    void Open(Uri uri);
}

public sealed class ExternalBrowserLauncher : IExternalBrowserLauncher
{
    public void Open(Uri uri)
    {
        Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }
}
