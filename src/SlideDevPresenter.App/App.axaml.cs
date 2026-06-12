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

            // Load settings synchronously during startup
            _settingsService.LoadAsync().GetAwaiter().GetResult();

            desktop.MainWindow = new MainWindow(_settingsService);
        }

        base.OnFrameworkInitializationCompleted();
    }
}