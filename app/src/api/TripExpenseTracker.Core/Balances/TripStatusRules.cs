namespace TripExpenseTracker.Core.Balances;

/// <summary>The settle state a trip card shows (F6.R19, F5.R10).</summary>
public enum SettleState
{
    None,
    ToSettle,
    Settled,
}

/// <summary>A trip card's settle status and the number of suggested payments still to make (F6.R19).</summary>
public sealed record TripStatus(SettleState State, int PaymentsToSettle)
{
    /// <summary>A trip with no expenses: no badge (F6.R19, F6.AC20).</summary>
    public static readonly TripStatus NoStatus = new(SettleState.None, 0);

    /// <summary>
    /// No expenses gives none. One or more suggestions give to-settle with that count. Otherwise the trip has expenses
    /// and every balance is zero, so it is settled (F6.R19, F6.AC17 to AC19).
    /// </summary>
    public static TripStatus From(int expenseCount, int suggestionCount)
    {
        if (expenseCount == 0)
        {
            return NoStatus;
        }

        return suggestionCount > 0
            ? new TripStatus(SettleState.ToSettle, suggestionCount)
            : new TripStatus(SettleState.Settled, 0);
    }
}
