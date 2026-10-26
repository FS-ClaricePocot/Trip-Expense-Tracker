namespace TripExpenseTracker.Core.Trips;

/// <summary>
/// Trip persistence. Every method takes the owner, and the implementation must filter on it, so one user's
/// trips are never read or changed through another user's id (F2.R9). Methods run on the unit of work of
/// the surrounding transaction when there is one.
/// </summary>
public interface ITripDao
{
    /// <summary>The owner's trips, newest first (F2.R5).</summary>
    Task<IReadOnlyList<Trip>> ListAsync(Guid owner, CancellationToken cancellationToken);

    /// <summary>One trip the owner owns, or null when it is missing or belongs to someone else.</summary>
    Task<Trip?> GetAsync(Guid owner, Guid id, CancellationToken cancellationToken);

    /// <summary>How many trips the owner has (F2.R4).</summary>
    Task<int> CountAsync(Guid owner, CancellationToken cancellationToken);

    /// <summary>Whether the owner already has a trip with this name, ignoring capitals (F2.R3).</summary>
    Task<bool> ExistsWithNameAsync(Guid owner, string name, CancellationToken cancellationToken);

    Task InsertAsync(Trip trip, CancellationToken cancellationToken);

    /// <summary>Deletes the owner's trip. Returns false when no row was removed (F2.R9, F2.R12).</summary>
    Task<bool> DeleteAsync(Guid owner, Guid id, CancellationToken cancellationToken);

    /// <summary>Locks the owner's user row for the rest of the transaction, so the owner's creates run one at a time (F2.R4).</summary>
    Task LockOwnerAsync(Guid owner, CancellationToken cancellationToken);
}
