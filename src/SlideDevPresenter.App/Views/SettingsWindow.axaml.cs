using Avalonia.Controls;
using Avalonia.Interactivity;
using SlideDevPresenter.App.ViewModels;

namespace SlideDevPresenter.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
