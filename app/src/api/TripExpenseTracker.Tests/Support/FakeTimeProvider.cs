namespace TripExpenseTracker.Tests.Support;

/// <summary>
/// Test clock. It starts at a fixed instant and moves only when the test calls <see cref="Advance"/>.
/// It never reads the real clock. Timers (used by the background cleanup wait, F1.R6) fire only when
/// <see cref="Advance"/> moves the clock past their due time.
/// </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private readonly object _lock = new();
    private readonly List<FakeTimer> _timers = new();
    private TaskCompletionSource _timerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DateTimeOffset _now;

    public FakeTimeProvider(DateTimeOffset start)
    {
        _now = start;
    }

    /// <summary>Completes the first time a timer is created, so a test can wait for a loop to start waiting before it advances the clock.</summary>
    public Task TimerCreated => _timerCreated.Task;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by)
    {
        List<FakeTimer> due;
        lock (_lock)
        {
            _now = _now.Add(by);
            due = _timers.Where(timer => timer.DueAt <= _now).ToList();
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state);
        timer.Change(dueTime, period);
        _timerCreated.TrySetResult();
        return timer;
    }

    private void Track(FakeTimer timer, bool track)
    {
        lock (_lock)
        {
            if (track)
            {
                if (!_timers.Contains(timer))
                {
                    _timers.Add(timer);
                }
            }
            else
            {
                _timers.Remove(timer);
            }
        }
    }

    /// <summary>A single-shot timer on the fake clock. The period is ignored, since only Task.Delay uses it.</summary>
    private sealed class FakeTimer : ITimer
    {
        private readonly FakeTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;

        public FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
        }

        public DateTimeOffset DueAt { get; private set; } = DateTimeOffset.MaxValue;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime == Timeout.InfiniteTimeSpan)
            {
                _owner.Track(this, track: false);
                return true;
            }

            DueAt = _owner.GetUtcNow() + dueTime;
            _owner.Track(this, track: true);
            return true;
        }

        public void Fire()
        {
            _owner.Track(this, track: false);
            _callback(_state);
        }

        public void Dispose() => _owner.Track(this, track: false);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
