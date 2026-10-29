namespace TripExpenseTracker.Core.Balances;

/// <summary>
/// What balances need for one trip, read in one owner-checked pass (F5.R11, F5.R12). <c>People</c> is in
/// <c>seq</c> order. <c>Expenses</c> carry the payer and the participants with their <c>seq</c>. Both lists are
/// empty when the trip is not the owner's or does not exist; the service tells those apart with the trip lookup (F2).
/// <c>Payments</c> (F5.R1, F6.R10) are the trip's recorded payments; null or empty means none. F5 tests omit them.
/// </summary>
public sealed record BalanceSnapshot(
    IReadOnlyList<BalancePerson> People,
    IReadOnlyList<BalanceExpense> Expenses,
    IReadOnlyList<BalancePayment>? Payments = null);

/// <summary>
/// Balance reads. The implementation must filter on the owner (the owner join in the WHERE clause), so a trip the
/// user does not own looks the same as a missing one (F5.R12, F5.AC12). F5 reads only; no writes (F5.R11).
/// </summary>
public interface IBalanceDao
{
    /// <summary>The trip's people and expenses for the owner. A trip with no people or expenses gives empty lists, not an error.</summary>
    Task<BalanceSnapshot> GetSnapshotAsync(Guid owner, Guid tripId, CancellationToken cancellationToken);
}
