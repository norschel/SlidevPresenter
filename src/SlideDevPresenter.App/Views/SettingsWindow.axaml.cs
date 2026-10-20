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

        try
        {
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
            if (stream.CanSeek)
            {
                stream.SetLength(0);
                stream.Position = 0;
            }
            await viewModel.ExportConfigurationAsync(stream);
        }
        catch (System.IO.IOException ex)
        {
            viewModel.ConfigurationMessage = $"Export failed: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            viewModel.ConfigurationMessage = $"Export failed: {ex.Message}";
        }
        catch (System.Text.Json.JsonException ex)
        {
            viewModel.ConfigurationMessage = $"Export failed: {ex.Message}";
        }
    }

    private async void Import_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
            return;

        try
        {
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
        catch (System.IO.IOException ex)
        {
            viewModel.ConfigurationMessage = $"Import failed: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            viewModel.ConfigurationMessage = $"Import failed: {ex.Message}";
        }
        catch (System.Text.Json.JsonException ex)
        {
            viewModel.ConfigurationMessage = $"Import failed: {ex.Message}";
        }
    }

    private async void ExportSourceList_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
            return;

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export source list",
                SuggestedFileName = "SlideDevPresenter-sources.json",
                DefaultExtension = "json",
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });
            if (file is null)
                return;

            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek)
            {
                stream.SetLength(0);
                stream.Position = 0;
            }
            await viewModel.ExportSourceListAsync(stream);
        }
        catch (System.IO.IOException ex)
        {
            viewModel.ConfigurationMessage = $"Export failed: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            viewModel.ConfigurationMessage = $"Export failed: {ex.Message}";
        }
    }

    private async void ImportSourceList_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
            return;

        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import source list",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });
            if (files.Count == 0)
                return;

            await using var stream = await files[0].OpenReadAsync();
            await viewModel.ImportSourceListAsync(stream);
        }
        catch (System.IO.IOException ex)
        {
            viewModel.ConfigurationMessage = $"Import failed: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            viewModel.ConfigurationMessage = $"Import failed: {ex.Message}";
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
