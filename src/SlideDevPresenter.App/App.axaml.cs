using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using SlideDevPresenter.App.Services;
using SlideDevPresenter.App.ViewModels;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());

            var settingsService = new SettingsService(loggerFactory.CreateLogger<SettingsService>());
            var sourceScanner = new SourceScanner(loggerFactory.CreateLogger<SourceScanner>());
            var processHost = new SlidevProcessHost(loggerFactory.CreateLogger<SlidevProcessHost>());
            var slideDeckMetadataReader = new SlideDeckMetadataReader(loggerFactory.CreateLogger<SlideDeckMetadataReader>());
            var displayService = new AvaloniaDisplayService();
            var presentationWindowService = new PresentationWindowService(displayService, settingsService);

            var mainViewModel = new MainViewModel(settingsService, sourceScanner, processHost, slideDeckMetadataReader, presentationWindowService);
            var mainWindow = new MainWindow(mainViewModel);
            desktop.MainWindow = mainWindow;

            mainWindow.Opened += async (_, _) =>
            {
                await settingsService.LoadAsync();
                await mainViewModel.RefreshLibraryAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}