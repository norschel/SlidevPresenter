using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SlideDevPresenter.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    public AboutWindow(string version) : this()
    {
        VersionText.Text = $"Version {version}";
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
