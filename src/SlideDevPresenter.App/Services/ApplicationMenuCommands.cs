using System.Windows.Input;
using Avalonia.Input;

namespace SlideDevPresenter.App.Services;

/// <summary>Commands and shortcuts the native macOS window menus (everything after the application menu) are bound to.</summary>
public sealed class ApplicationMenuCommands
{
    public required ICommand RefreshLibrary { get; init; }
    public required ICommand ImportLibrary { get; init; }
    public required ICommand ExportLibrary { get; init; }
    public required ICommand OpenDeckLocation { get; init; }
    public required ICommand CopyDeckLocation { get; init; }
    public required ICommand OpenInVsCode { get; init; }
    public required ICommand ExportPdf { get; init; }
    public required ICommand RemoveFromLibrary { get; init; }

    public required ICommand StartFromBeginning { get; init; }
    public required ICommand StartFromCurrentSlide { get; init; }
    public required ICommand StartPresenterView { get; init; }
    public KeyGesture? StartFromBeginningGesture { get; init; }
    public KeyGesture? StartFromCurrentSlideGesture { get; init; }
    public KeyGesture? StartPresenterViewGesture { get; init; }
    public required ICommand Resume { get; init; }
    public required ICommand Stop { get; init; }
    public required ICommand Retry { get; init; }
    public required ICommand SwapDisplays { get; init; }
    public required ICommand DetectDisplays { get; init; }

    public required ICommand ShowHomeTab { get; init; }
    public required ICommand ShowLibraryTab { get; init; }
    public required ICommand ShowViewTab { get; init; }
    public required ICommand ShowPresentationTab { get; init; }
    public required ICommand ShowBrowserTab { get; init; }
    public required ICommand UsePresenterSurface { get; init; }
    public required ICommand UseParticipantSurface { get; init; }
    public required ICommand ToggleThumbnailsPanel { get; init; }
    public required ICommand ToggleAgendaPanel { get; init; }
    public required ICommand ToggleTimerPanel { get; init; }
    public required ICommand RestoreChrome { get; init; }
    public required ICommand OpenCurrentViewInBrowser { get; init; }
    public required ICommand OpenPresenterInBrowser { get; init; }
    public required ICommand OpenParticipantInBrowser { get; init; }

    public required ICommand Minimize { get; init; }
    public required ICommand Zoom { get; init; }

    public required ICommand ShowKeyboardShortcuts { get; init; }
    public required ICommand OpenDocumentation { get; init; }
    public required ICommand ReportIssue { get; init; }
}
