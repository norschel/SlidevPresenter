using Avalonia.Input;

namespace SlideDevPresenter.App.Services;

public enum PresentationShortcutAction
{
    StartFromBeginning,
    StartFromCurrentSlide,
    StartPresenterView
}

public interface IShortcutService
{
    KeyGesture GetGesture(PresentationShortcutAction action);
    string GetDisplayText(PresentationShortcutAction action);
}

public interface IPlatformInfo
{
    bool IsMacOS { get; }
}

public sealed class RuntimePlatformInfo : IPlatformInfo
{
    public bool IsMacOS => OperatingSystem.IsMacOS();
}

public sealed class ShortcutService(IPlatformInfo platformInfo) : IShortcutService
{
    private readonly IPlatformInfo _platformInfo = platformInfo;

    public KeyGesture GetGesture(PresentationShortcutAction action)
    {
        if (_platformInfo.IsMacOS)
        {
            return action switch
            {
                PresentationShortcutAction.StartFromBeginning => new KeyGesture(Key.Enter, KeyModifiers.Meta | KeyModifiers.Shift),
                PresentationShortcutAction.StartFromCurrentSlide => new KeyGesture(Key.Enter, KeyModifiers.Meta),
                PresentationShortcutAction.StartPresenterView => new KeyGesture(Key.Enter, KeyModifiers.Alt),
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        return action switch
        {
            PresentationShortcutAction.StartFromBeginning => new KeyGesture(Key.F5),
            PresentationShortcutAction.StartFromCurrentSlide => new KeyGesture(Key.F5, KeyModifiers.Shift),
            PresentationShortcutAction.StartPresenterView => new KeyGesture(Key.F5, KeyModifiers.Alt),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
    }

    public string GetDisplayText(PresentationShortcutAction action)
    {
        if (_platformInfo.IsMacOS)
        {
            return action switch
            {
                PresentationShortcutAction.StartFromBeginning => "⌘+Shift+Return",
                PresentationShortcutAction.StartFromCurrentSlide => "⌘+Return",
                PresentationShortcutAction.StartPresenterView => "Option+Return",
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        return action switch
        {
            PresentationShortcutAction.StartFromBeginning => "F5",
            PresentationShortcutAction.StartFromCurrentSlide => "Shift+F5",
            PresentationShortcutAction.StartPresenterView => "Alt+F5",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
    }
}
