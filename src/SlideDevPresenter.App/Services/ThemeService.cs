using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;

namespace SlideDevPresenter.App.Services;

public interface IThemeService
{
    void ApplyTheme(string? theme);
}

public sealed class ThemeService : IThemeService
{
    public void ApplyTheme(string? theme)
    {
        if (Application.Current is null)
            return;

        var variant = ToThemeVariant(theme);
        Application.Current.RequestedThemeVariant = variant;

        if (Application.Current.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        foreach (var window in desktop.Windows)
            window.RequestedThemeVariant = variant;
    }

    internal static ThemeVariant ToThemeVariant(string? theme) =>
        theme?.Trim().ToLowerInvariant() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
}
