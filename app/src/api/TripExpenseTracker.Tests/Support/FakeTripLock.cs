using TripExpenseTracker.Core.Payments;

namespace TripExpenseTracker.Tests.Support;

/// <summary>
/// Fake <see cref="ITripLock"/> (F6.R18). Answers true only for the (owner, trip) pairs it was given, as the real lock
/// does for the owner's trip, and counts the calls so a test can check that a write took the lock.
/// </summary>
public sealed class FakeTripLock : ITripLock
{
    private readonly List<(Guid Owner, Guid TripId)> _owned = [];

    /// <summary>How many times the lock was taken.</summary>
    public int CallCount { get; private set; }

    public void Own(Guid owner, Guid tripId) => _owned.Add((owner, tripId));

    public Task<bool> LockAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(_owned.Contains((owner, tripId)));
    }
}
