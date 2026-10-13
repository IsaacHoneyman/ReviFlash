using ReviFlash.ViewModels;

namespace ReviFlash.Tests.ViewModels;

public class ActiveTimeCounterTests
{
    private DateTime _now = new(2026, 10, 13, 9, 0, 0);
    private readonly List<int> _saved = [];
    private bool _saveWorks = true;

    private ActiveTimeCounter NewCounter() => new(seconds =>
    {
        if (_saveWorks) _saved.Add(seconds);
        return _saveWorks;
    }, () => _now);

    private void Wait(TimeSpan time) => _now += time;

    [Fact]
    public void Each_tick_while_in_use_counts_five_seconds()
    {
        var counter = NewCounter();

        counter.Tick();
        counter.Tick();

        Assert.Equal(10, counter.UnsavedSeconds);
    }

    [Fact]
    public void Time_stops_counting_after_two_minutes_without_input()
    {
        var counter = NewCounter();

        Wait(TimeSpan.FromMinutes(2));
        counter.Tick();

        Assert.Equal(0, counter.UnsavedSeconds);
    }

    [Fact]
    public void Input_after_going_idle_starts_the_count_again()
    {
        var counter = NewCounter();
        Wait(TimeSpan.FromMinutes(5));
        counter.Tick();

        counter.Activity();
        counter.Tick();

        Assert.Equal(5, counter.UnsavedSeconds);
    }

    [Fact]
    public void Nothing_counts_while_another_window_is_focused()
    {
        var counter = NewCounter();
        counter.IsWindowActive = false;

        counter.Tick();

        Assert.Equal(0, counter.UnsavedSeconds);
    }

    [Fact]
    public void A_full_minute_is_saved_as_it_builds_up()
    {
        var counter = NewCounter();

        for (int i = 0; i < 12; i++) counter.Tick();

        Assert.Equal([60], _saved);
        Assert.Equal(0, counter.UnsavedSeconds);
    }

    [Fact]
    public void Stopping_saves_whatever_is_left()
    {
        var counter = NewCounter();
        counter.Tick();

        counter.Stop();

        Assert.Equal([5], _saved);
    }

    [Fact]
    public void Time_that_could_not_be_saved_is_kept_for_the_next_try()
    {
        var counter = NewCounter();
        _saveWorks = false;
        counter.Tick();
        counter.Save();

        _saveWorks = true;
        counter.Tick();
        counter.Save();

        Assert.Equal([10], _saved);
    }

    [Fact]
    public void Discarded_time_is_never_saved()
    {
        var counter = NewCounter();
        counter.Tick();

        counter.Discard();
        counter.Stop();

        Assert.Empty(_saved);
    }
}
