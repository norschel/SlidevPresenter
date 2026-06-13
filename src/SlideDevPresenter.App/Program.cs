using Avalonia;
using System;
using System.Diagnostics;
using System.IO;

namespace SlideDevPresenter.App;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        ConfigureWebView2UserDataFolder();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static void ConfigureWebView2UserDataFolder()
    {
        // WebView2 defaults can resolve to a non-writable location in some launch contexts,
        // which causes E_ACCESSDENIED during NativeWebView creation.
        const string userDataEnvVar = "WEBVIEW2_USER_DATA_FOLDER";

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(userDataEnvVar)))
            return;

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (string.IsNullOrWhiteSpace(localAppData))
                return;

            var userDataFolder = Path.Combine(localAppData, "SlideDevPresenter", "WebView2");
            Directory.CreateDirectory(userDataFolder);
            Environment.SetEnvironmentVariable(userDataEnvVar, userDataFolder);
        }
        catch (Exception ex)
        {
            // Non-fatal: app can still run with external-browser fallback when embedding fails.
            Trace.WriteLine($"Failed to configure {userDataEnvVar}: {ex}");
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
