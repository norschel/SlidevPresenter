using Avalonia.Input;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

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

public sealed class ShortcutService : IShortcutService
{
    private readonly IPlatformInfo _platformInfo;
    private readonly ISettingsService? _settingsService;

    public ShortcutService(IPlatformInfo platformInfo, ISettingsService? settingsService = null)
    {
        _platformInfo = platformInfo;
        _settingsService = settingsService;
    }

    public KeyGesture GetGesture(PresentationShortcutAction action)
    {
        var custom = GetCustomGestureString(action);
        if (custom is not null)
        {
            try { return KeyGesture.Parse(custom); }
            catch { /* fall through to default */ }
        }

        return GetDefaultGesture(action);
    }

    public string GetDisplayText(PresentationShortcutAction action)
    {
        var custom = GetCustomGestureString(action);
        if (custom is not null)
        {
            try
            {
                var g = KeyGesture.Parse(custom);
                return g.ToString();
            }
            catch { /* fall through to default */ }
        }

        return GetDefaultDisplayText(action);
    }

    private string? GetCustomGestureString(PresentationShortcutAction action)
    {
        var shortcuts = _settingsService?.Settings.Shortcuts;
        if (shortcuts is null)
            return null;

        return action switch
        {
            PresentationShortcutAction.StartFromBeginning => shortcuts.StartFromBeginning,
            PresentationShortcutAction.StartFromCurrentSlide => shortcuts.StartFromCurrentSlide,
            PresentationShortcutAction.StartPresenterView => shortcuts.StartPresenterView,
            _ => null
        };
    }

    private KeyGesture GetDefaultGesture(PresentationShortcutAction action)
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

    private string GetDefaultDisplayText(PresentationShortcutAction action)
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
