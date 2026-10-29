using TripExpenseTracker.Core.Balances;
using TripExpenseTracker.Core.SettleUp;

namespace TripExpenseTracker.Core.Payments;

/// <summary>
/// Balances with payments applied (F5.R1, F6.R10), and the suggestions worked out from them (F6.R2, F6.R11).
/// Shared by the mark-as-paid check and the settle-up read, so both use the same numbers. Pure: no I/O.
/// </summary>
internal static class SettleUpMath
{
    public static IReadOnlyList<PersonBalance> Balances(BalanceSnapshot snapshot, IReadOnlyList<StoredPayment> payments)
    {
        var balancePayments = payments
            .Select(p => new BalancePayment(p.Payment.PayerPersonId, p.Payment.PayeePersonId, p.Payment.AmountCents))
            .ToList();

        return BalanceCalculator.Calculate(snapshot.People, snapshot.Expenses, balancePayments);
    }

    public static IReadOnlyList<SuggestedPayment> Suggest(IReadOnlyList<PersonBalance> balances, IReadOnlyList<BalancePerson> people)
    {
        var seqById = people.ToDictionary(p => p.Id, p => p.Seq);
        var input = balances
            .Select(b => (b.PersonId, seqById[b.PersonId], b.BalanceCents))
            .ToList();

        return SuggestionRules.Compute(input);
    }
}
