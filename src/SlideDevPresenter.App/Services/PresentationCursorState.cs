namespace SlideDevPresenter.App.Services;

/// <summary>Per-surface cursor lifecycle; time is supplied by the host's monotonic clock.</summary>
public sealed class PresentationCursorState
{
    public static readonly TimeSpan InactivityTimeout = TimeSpan.FromSeconds(60);
    public bool IsActive { get; private set; }
    public bool IsHidden { get; private set; }
    private TimeSpan _lastActivity;

    public void Start(TimeSpan now)
    {
        IsActive = true;
        IsHidden = true;
        _lastActivity = now;
    }

    public void Stop()
    {
        IsActive = false;
        IsHidden = false;
    }

    public void Move(TimeSpan now)
    {
        if (!IsActive) return;
        IsHidden = false;
        _lastActivity = now;
    }

    public void Toggle(TimeSpan now)
    {
        if (!IsActive) return;
        IsHidden = !IsHidden;
        _lastActivity = now;
    }

    public void Tick(TimeSpan now)
    {
        if (IsActive && now - _lastActivity >= InactivityTimeout)
            IsHidden = true;
    }

    public TimeSpan? GetRemainingInactivity(TimeSpan now) =>
        IsActive && !IsHidden
            ? TimeSpan.FromTicks(Math.Max(0, (InactivityTimeout - (now - _lastActivity)).Ticks))
            : null;
}
