using TripExpenseTracker.Core.Payments;
using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Core.Balances;

/// <summary>
/// Balances and the trip summary (F5.R1 to R3, R7, R8, R11, R12). Every call is for one owner. The trip must belong
/// to the owner before balances are read: <see cref="IBalanceDao"/> returns empty lists for a foreign or missing trip,
/// and the service must answer "not found" for both (F5.R12). Nothing is cached, so each call reads the current
/// expenses (F5.R7).
/// </summary>
public sealed class BalanceService
{
    private readonly IBalanceDao _balances;
    private readonly ITripDao _trips;
    private readonly Func<IEnumerable<PersonBalance>, BalanceFailure?> _verify;

    /// <param name="verify">The zero-sum check. Defaults to <see cref="BalanceCheck.Verify"/>; tests pass a failing one (F5.AC16).</param>
    public BalanceService(IBalanceDao balances, ITripDao trips, Func<IEnumerable<PersonBalance>, BalanceFailure?>? verify = null)
    {
        _balances = balances;
        _trips = trips;
        _verify = verify ?? BalanceCheck.Verify;
    }

    /// <summary>The balances in <c>seq</c> order, or the refusal when the trip is not the owner's or the zero-sum check fails (F5.R3, F5.R12).</summary>
    public async Task<BalanceListResult> GetBalancesAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
    {
        var snapshot = await ReadAsync(owner, tripId, cancellationToken);
        if (snapshot is null)
        {
            return BalanceListResult.Refused(BalanceServiceFailure.NotFound);
        }

        var balances = BalanceCalculator.Calculate(snapshot.People, snapshot.Expenses, PaymentsOf(snapshot));
        if (_verify(balances) is not null)
        {
            return BalanceListResult.Refused(BalanceServiceFailure.BalancesInconsistent, snapshot.People.Count);
        }

        return BalanceListResult.Found(balances);
    }

    /// <summary>Total spent and left to settle (F5.R8), or <see cref="BalanceServiceFailure.NotFound"/> for a trip the owner does not have.</summary>
    public async Task<BalanceSummaryResult> GetSummaryAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
    {
        var snapshot = await ReadAsync(owner, tripId, cancellationToken);
        if (snapshot is null)
        {
            return BalanceSummaryResult.Refused(BalanceServiceFailure.NotFound);
        }

        var balances = BalanceCalculator.Calculate(snapshot.People, snapshot.Expenses, PaymentsOf(snapshot));
        // The "in {n} payments" count under Left to settle (F5.R8, F6.R2): the number of suggestions from the same balances.
        var suggestionCount = SettleUpMath.Suggest(balances, snapshot.People).Count;
        return BalanceSummaryResult.Found(BalanceSummary.Compute(snapshot.Expenses, balances), suggestionCount);
    }

    private static IReadOnlyList<BalancePayment> PaymentsOf(BalanceSnapshot snapshot) =>
        snapshot.Payments ?? Array.Empty<BalancePayment>();

    /// <summary>The owner's snapshot, or null when the trip is missing or belongs to someone else.</summary>
    private async Task<BalanceSnapshot?> ReadAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
    {
        if (await _trips.GetAsync(owner, tripId, cancellationToken) is null)
        {
            return null;
        }

        return await _balances.GetSnapshotAsync(owner, tripId, cancellationToken);
    }
}
