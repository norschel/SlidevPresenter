using Avalonia.Controls;
using Avalonia.Interactivity;
using SlideDevPresenter.App.ViewModels;
using SlideDevPresenter.App.Views;

namespace SlideDevPresenter.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel? _viewModel;

    /// <summary>Parameterless constructor for Avalonia designer.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void Settings_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        var vm = new SettingsViewModel(_viewModel.SettingsService);
        var win = new SettingsWindow(vm);
        win.ShowDialog(this);
    }
}