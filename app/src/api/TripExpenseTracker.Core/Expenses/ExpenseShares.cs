namespace TripExpenseTracker.Core.Expenses;

/// <summary>One participant's share of an expense, in cents. A share of 0 is valid for a payer who is not a participant.</summary>
public sealed record ExpenseShare(Guid PersonId, long Cents);

/// <summary>
/// Equal split to the cent (F4.R7). Each share is the amount divided by the participant count, rounded down.
/// The leftover cents go one each to the earliest-added participants (lowest <c>seq</c>). The shares add up to the amount.
/// Pure logic, no I/O.
/// </summary>
public static class ExpenseShares
{
    /// <summary>Returns one share per participant in <c>seq</c> order. Refuses an empty participant list (F4.R5).</summary>
    public static IReadOnlyList<ExpenseShare> Compute(long amountCents, IReadOnlyList<(Guid PersonId, long Seq)> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        if (participants.Count == 0)
        {
            throw new ArgumentException("An expense needs at least one participant.", nameof(participants));
        }

        var ordered = participants.OrderBy(p => p.Seq).ToList();
        var count = ordered.Count;
        var baseShare = amountCents / count;
        var leftover = amountCents % count;

        var shares = new List<ExpenseShare>(count);
        for (var i = 0; i < count; i++)
        {
            var extra = i < leftover ? 1L : 0L;
            shares.Add(new ExpenseShare(ordered[i].PersonId, baseShare + extra));
        }

        return shares;
    }
}
