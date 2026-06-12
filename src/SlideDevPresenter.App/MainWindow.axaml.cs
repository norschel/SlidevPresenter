using Avalonia.Controls;
using Avalonia.Interactivity;
using SlideDevPresenter.App.ViewModels;
using SlideDevPresenter.App.Views;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.App;

public partial class MainWindow : Window
{
    private readonly ISettingsService _settingsService;

    public MainWindow() : this(null!) { }

    public MainWindow(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeComponent();
    }

    private void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var vm = new SettingsViewModel(_settingsService);
        var win = new SettingsWindow(vm);
        win.ShowDialog(this);
    }
}