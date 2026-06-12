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
        try
        {
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open external URI '{uri}': {ex.Message}");
        }
    }
}
