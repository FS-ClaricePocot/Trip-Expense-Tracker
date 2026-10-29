namespace TripExpenseTracker.Core.SettleUp;

/// <summary>
/// The Settle up progress tiles (F6.R12). <see cref="Percentage"/> is null when nothing is owed (F6.AC14).
/// </summary>
public sealed record Progress(long PaidBackCents, long OwedCents, int? Percentage)
{
    /// <summary>
    /// Owed is paid back plus left to settle. The percentage is rounded down, in <see cref="Int128"/>,
    /// so it reads 100 only when <paramref name="leftToSettleCents"/> is 0 (F6.R12, F6.AC10).
    /// </summary>
    public static Progress Compute(long paidBackCents, long leftToSettleCents)
    {
        if (paidBackCents < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paidBackCents), "Paid back cannot be negative.");
        }

        if (leftToSettleCents < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(leftToSettleCents), "Left to settle cannot be negative.");
        }

        var owed = paidBackCents + leftToSettleCents;
        if (owed == 0)
        {
            return new Progress(paidBackCents, 0, null);
        }

        var percentage = (int)((Int128)paidBackCents * 100 / owed);
        return new Progress(paidBackCents, owed, percentage);
    }
}
