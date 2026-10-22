using Avalonia.Controls;
using Avalonia.Interactivity;
using SlideDevPresenter.App.ViewModels;

namespace SlideDevPresenter.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    public AboutWindow(string version, AppUpdateViewModel updates) : this()
    {
        VersionText.Text = $"Version {version}";
        UpdatePanel.DataContext = updates;
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
