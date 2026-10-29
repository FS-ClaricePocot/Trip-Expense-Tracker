namespace TripExpenseTracker.Core.Payments;

/// <summary>
/// Payment persistence (F6.R6, R7, R8). Every method takes the owner, and the implementation must filter on it
/// (the owner join in the WHERE clause), so a trip the user does not own looks the same as a missing one (F6.R15).
/// Methods run on the unit of work of the surrounding transaction. Callers take <see cref="ITripLock"/> first.
/// </summary>
public interface IPaymentDao
{
    /// <summary>The trip's payments with the payer and payee names, in any order. Empty when the trip is not the owner's.</summary>
    Task<IReadOnlyList<StoredPayment>> ListAsync(Guid owner, Guid tripId, CancellationToken cancellationToken);

    /// <summary>Adds the payment to the owner's trip. Returns false when no row was written (trip or person not found).</summary>
    Task<bool> InsertAsync(Guid owner, Payment payment, CancellationToken cancellationToken);

    /// <summary>Deletes the payment if it is on the owner's trip (F6.R8, R15). Returns false when no row was removed.</summary>
    Task<bool> DeleteAsync(Guid owner, Guid tripId, Guid paymentId, CancellationToken cancellationToken);
}
