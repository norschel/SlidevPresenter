using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using SlideDevPresenter.App.Views;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.App.Services;

/// <summary>Prompts the user with a modal dialog to choose the display for the participant view.</summary>
public sealed class AvaloniaDisplaySelector : IDisplaySelector
{
    public async Task<DisplayInfo?> SelectDisplayAsync(IReadOnlyList<DisplayInfo> displays, CancellationToken cancellationToken = default)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
                return displays.FirstOrDefault(d => !d.IsPrimary) ?? displays.FirstOrDefault();

            var dialog = new DisplaySelectionWindow(displays);
            return await dialog.ShowDialogAsync(owner);
        });
    }
}
