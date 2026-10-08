using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export configuration",
            SuggestedFileName = "SlideDevPresenter-settings.json",
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
        });
        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await viewModel.ExportConfigurationAsync(stream);
    }

    private async void Import_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import configuration",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
        });
        if (files.Count == 0)
            return;

        await using var stream = await files[0].OpenReadAsync();
        await viewModel.ImportConfigurationAsync(stream);
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
