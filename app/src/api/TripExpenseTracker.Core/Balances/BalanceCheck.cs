namespace TripExpenseTracker.Core.Balances;

/// <summary>Why balances cannot be shown. Maps to the ProblemDetails type <c>balances_inconsistent</c> (F5.R3).</summary>
public enum BalanceFailure
{
    BalancesInconsistent,
}

/// <summary>The zero-sum check (F5.R3, F5.AC16). Pure logic, no I/O.</summary>
public static class BalanceCheck
{
    /// <summary>Returns <see cref="BalanceFailure.BalancesInconsistent"/> unless the balances add up to exactly 0, else <c>null</c>.</summary>
    public static BalanceFailure? Verify(IEnumerable<PersonBalance> balances)
    {
        ArgumentNullException.ThrowIfNull(balances);

        var total = balances.Sum(b => b.BalanceCents);
        return total == 0 ? null : BalanceFailure.BalancesInconsistent;
    }
}
