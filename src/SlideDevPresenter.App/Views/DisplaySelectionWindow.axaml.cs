using Avalonia.Controls;
using Avalonia.Interactivity;
using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.App.Views;

public partial class DisplaySelectionWindow : Window
{
    /// <summary>Parameterless constructor for the Avalonia designer.</summary>
    public DisplaySelectionWindow()
    {
        InitializeComponent();
    }

    public DisplaySelectionWindow(IReadOnlyList<DisplayInfo> displays)
    {
        InitializeComponent();
        DisplayList.ItemsSource = displays;
        DisplayList.SelectedItem = displays.FirstOrDefault(d => !d.IsPrimary) ?? displays.FirstOrDefault();
        DisplayList.DoubleTapped += (_, _) => Ok_Click(this, new RoutedEventArgs());
        DisplayList.SelectionChanged += (_, _) => OkButton.IsEnabled = DisplayList.SelectedItem is not null;
    }

    /// <summary>Shows the dialog and returns the chosen display, or <c>null</c> if the user declined.</summary>
    public Task<DisplayInfo?> ShowDialogAsync(Window owner) => ShowDialog<DisplayInfo?>(owner);

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        if (DisplayList.SelectedItem is DisplayInfo display)
            Close(display);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
