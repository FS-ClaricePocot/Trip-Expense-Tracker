namespace TripExpenseTracker.Tests.Support;

/// <summary>
/// Test clock. It starts at a fixed instant and moves only when the test calls <see cref="Advance"/>.
/// It never reads the real clock.
/// </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FakeTimeProvider(DateTimeOffset start)
    {
        _now = start;
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
