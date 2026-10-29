namespace TripExpenseTracker.Core.Payments;

/// <summary>
/// Takes a row lock on the owner's trip for the rest of the surrounding transaction (F6.R18). Every write that changes
/// payments, and every expense write, calls this first, so two simultaneous "Mark as paid" calls cannot record the same
/// suggestion twice. Core cannot reference the Data project, so TripExpenseTracker.Data implements this.
/// </summary>
public interface ITripLock
{
    /// <summary>True when the trip belongs to the owner and is now locked. False for a foreign or missing trip (F6.R15).</summary>
    Task<bool> LockAsync(Guid owner, Guid tripId, CancellationToken cancellationToken);
}
