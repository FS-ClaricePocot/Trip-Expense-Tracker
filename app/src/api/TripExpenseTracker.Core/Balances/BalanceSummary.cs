namespace TripExpenseTracker.Core.Balances;

/// <summary>The trip page tiles (F5.R8): total of all expenses, and the sum of positive balances.</summary>
public sealed record BalanceSummary(long TotalSpentCents, long LeftToSettleCents)
{
    /// <summary>
    /// Total spent is the sum of all expense amounts (F4.R13). Left to settle is the sum of all positive balances (F5.R8).
    /// Pure logic, no I/O.
    /// </summary>
    public static BalanceSummary Compute(IEnumerable<BalanceExpense> expenses, IEnumerable<PersonBalance> balances)
    {
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(balances);

        var totalSpent = expenses.Sum(e => e.AmountCents);
        var leftToSettle = balances.Where(b => b.BalanceCents > 0).Sum(b => b.BalanceCents);
        return new BalanceSummary(totalSpent, leftToSettle);
    }
}
