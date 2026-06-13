using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using SlideDevPresenter.App.Services;
using SlideDevPresenter.App.ViewModels;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.App;

public partial class App : Application
{
    private SettingsService? _settingsService;
    private MainViewModel? _mainViewModel;
    private MainWindow? _mainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());

            _settingsService = new SettingsService(loggerFactory.CreateLogger<SettingsService>());
            var sourceScanner = new SourceScanner(loggerFactory.CreateLogger<SourceScanner>());
            var processHost = new SlidevProcessHost(loggerFactory.CreateLogger<SlidevProcessHost>());
            var slideDeckMetadataReader = new SlideDeckMetadataReader(loggerFactory.CreateLogger<SlideDeckMetadataReader>());
            var displayService = new AvaloniaDisplayService();
            var presentationWindowService = new PresentationWindowService(displayService, _settingsService);
            var themeService = new ThemeService();
            var shortcutService = new ShortcutService(new RuntimePlatformInfo(), _settingsService);

            Dispatcher.UIThread.UnhandledException += OnUiThreadUnhandledException;

            _mainViewModel = new MainViewModel(_settingsService, sourceScanner, processHost, slideDeckMetadataReader, presentationWindowService, displayService);
            _mainWindow = new MainWindow(_mainViewModel, shortcutService, themeService);

            presentationWindowService.PresentationExited += (_, _) => _mainWindow.Activate();
            desktop.MainWindow = _mainWindow;

            desktop.Exit += (_, _) => Dispatcher.UIThread.UnhandledException -= OnUiThreadUnhandledException;

            _mainWindow.Opened += async (_, _) =>
            {
                await _settingsService.LoadAsync();
                themeService.ApplyTheme(_settingsService.Settings.Appearance.Theme);
                await _mainViewModel.RefreshLibraryAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnUiThreadUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (!IsRecoverableEmbeddedWebViewAccessDenied(e.Exception))
            return;

        if (_settingsService is not null)
        {
            _settingsService.Settings.WebView.PreferEmbeddedWebView = false;
            _settingsService.Settings.WebView.AllowExternalBrowserFallback = true;
        }

        if (_mainViewModel is not null)
        {
            _mainViewModel.RefreshPreferences();
            _mainViewModel.SelectPresentationRibbon();
            _mainViewModel.ErrorMessage = "Embedded browser was disabled for this session because WebView access was denied. Use external browser fallback.";
        }

        _mainWindow?.DisableEmbeddedWebViewsForSession();
        e.Handled = true;
    }

    private static bool IsRecoverableEmbeddedWebViewAccessDenied(Exception exception)
    {
        const int accessDeniedHresult = unchecked((int)0x80070005);

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is UnauthorizedAccessException && current.HResult == accessDeniedHresult)
                return true;
        }

        return false;
    }
}