namespace SlideDevPresenter.Core.Models;

public sealed record DisplayInfo(int Index, bool IsPrimary, int X, int Y, int Width, int Height, string? Name = null)
{
    public string DisplayLabel
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(Name) ? $"Display {Index + 1}" : Name;
            var primary = IsPrimary ? " (Primary)" : string.Empty;
            return $"{name}{primary} — {Width}×{Height} at {X},{Y}";
        }
    }
}
