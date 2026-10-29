using TripExpenseTracker.Core.Balances;
using TripExpenseTracker.Core.SettleUp;
using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Core.Payments;

/// <summary>
/// The Settle up read (F6.R1, R7, R11, R12, R14, R15): suggestions, payments newest first, and the three tiles.
/// Every call is for one owner. The trip must belong to the owner first, so a foreign or missing trip gives
/// <see cref="PaymentFailure.NotFound"/>. Nothing is cached, so each call reads the current balances.
/// </summary>
public sealed class SettleUpService
{
    private readonly ITripDao _trips;
    private readonly IBalanceDao _balances;
    private readonly IPaymentDao _payments;

    public SettleUpService(ITripDao trips, IBalanceDao balances, IPaymentDao payments)
    {
        _trips = trips;
        _balances = balances;
        _payments = payments;
    }

    public async Task<SettleUpResult> GetAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
    {
        if (await _trips.GetAsync(owner, tripId, cancellationToken) is null)
        {
            return SettleUpResult.Refused(PaymentFailure.NotFound);
        }

        var snapshot = await _balances.GetSnapshotAsync(owner, tripId, cancellationToken);
        var payments = (await _payments.ListAsync(owner, tripId, cancellationToken))
            .OrderByDescending(p => p.Seq)
            .ToList();

        var balances = SettleUpMath.Balances(snapshot, payments);
        var suggestions = SettleUpMath.Suggest(balances, snapshot.People);
        var names = balances.ToDictionary(b => b.PersonId, b => b.Name);
        var named = suggestions
            .Select(s => new SettleUpSuggestion(
                s.PayerPersonId,
                names[s.PayerPersonId],
                s.PayeePersonId,
                names[s.PayeePersonId],
                s.AmountCents))
            .ToList();

        var summary = BalanceSummary.Compute(snapshot.Expenses, balances);
        var paidBack = payments.Sum(p => p.Payment.AmountCents);
        var progress = Progress.Compute(paidBack, summary.LeftToSettleCents);

        var view = new SettleUpView(
            named,
            payments,
            progress,
            summary.LeftToSettleCents,
            summary.TotalSpentCents,
            snapshot.Expenses.Count);

        return SettleUpResult.Found(view);
    }
}
