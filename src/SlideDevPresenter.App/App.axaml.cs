using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.App;

public partial class App : Application
{
    private SettingsService? _settingsService;

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

            var mainWindow = new MainWindow(_settingsService);
            desktop.MainWindow = mainWindow;

            // Load settings asynchronously after the application is ready
            mainWindow.Opened += async (_, _) =>
            {
                await _settingsService.LoadAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}