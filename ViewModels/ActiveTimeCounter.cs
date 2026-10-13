using System;
using Avalonia.Threading;

namespace ReviFlash.ViewModels;

/// <summary> Counts time spent in a window while it's focused and in use, stopping after two idle minutes, and saves it in batches. </summary>
public sealed class ActiveTimeCounter
{
    public static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(2);
    public const int TickSeconds = 5;
    public const int SaveEverySeconds = 60;

    private readonly Func<int, bool> _save;
    private readonly Func<DateTime> _now;
    private DispatcherTimer? _timer;
    private DateTime _lastActivity;

    /// <summary> Counted but not saved yet. </summary>
    public int UnsavedSeconds { get; private set; }

    /// <summary> Set from the window, so time only counts while ReviFlash is the focused window. </summary>
    public bool IsWindowActive { get; set; } = true;

    /// <param name="save"> Stores the seconds, returning false to keep them for the next try. </param>
    /// <param name="now"> The clock, replaceable in tests. </param>
    public ActiveTimeCounter(Func<int, bool> save, Func<DateTime>? now = null)
    {
        _save = save;
        _now = now ?? (() => DateTime.Now);
        _lastActivity = _now();
    }

    public void Start()
    {
        _timer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(TickSeconds) };
        _timer.Tick -= Timer_Tick;
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    /// <summary> Stops counting and saves what's left. </summary>
    public void Stop()
    {
        _timer?.Stop();
        Save();
    }

    /// <summary> Typing, clicking or scrolling: keeps the time counting. </summary>
    public void Activity() => _lastActivity = _now();

    private void Timer_Tick(object? sender, EventArgs e) => Tick();

    public void Tick()
    {
        if (IsWindowActive && _now() - _lastActivity < IdleAfter) UnsavedSeconds += TickSeconds;
        if (UnsavedSeconds >= SaveEverySeconds) Save();
    }

    public void Save()
    {
        if (UnsavedSeconds > 0 && _save(UnsavedSeconds)) UnsavedSeconds = 0;
    }

    /// <summary> Forgets what's been counted, e.g. when the work it belonged to was cancelled. </summary>
    public void Discard()
    {
        _timer?.Stop();
        UnsavedSeconds = 0;
    }
}
