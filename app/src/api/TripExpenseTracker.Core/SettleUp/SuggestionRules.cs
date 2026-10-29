namespace TripExpenseTracker.Core.SettleUp;

/// <summary>
/// The suggested payments rule of product.md R6 (F6.R2, F6.R3, F6.R4). Pure: no I/O, no clock.
/// Input is one entry per person: the person, their <c>seq</c> (the order they were added to the trip)
/// and their balance in cents (positive is owed, negative is owes).
/// </summary>
public static class SuggestionRules
{
    /// <summary>
    /// Repeats until every balance is zero: the largest debtor pays the largest creditor the smaller of the two amounts.
    /// Ties go to the lowest <c>seq</c> (F6.R3). Each step zeroes at least one balance, so the loop ends.
    /// The output is deterministic for the same input.
    /// </summary>
    public static IReadOnlyList<SuggestedPayment> Compute(IReadOnlyList<(Guid PersonId, long Seq, long BalanceCents)> balances)
    {
        ArgumentNullException.ThrowIfNull(balances);

        var working = balances
            .Where(b => b.BalanceCents != 0)
            .Select(b => new Working(b.PersonId, b.Seq, b.BalanceCents))
            .ToList();

        var suggestions = new List<SuggestedPayment>();

        while (true)
        {
            var debtor = working
                .Where(w => w.Balance < 0)
                .OrderBy(w => w.Balance)
                .ThenBy(w => w.Seq)
                .FirstOrDefault();
            var creditor = working
                .Where(w => w.Balance > 0)
                .OrderByDescending(w => w.Balance)
                .ThenBy(w => w.Seq)
                .FirstOrDefault();

            if (debtor is null || creditor is null)
            {
                break;
            }

            var amount = Math.Min(-debtor.Balance, creditor.Balance);
            suggestions.Add(new SuggestedPayment(debtor.PersonId, creditor.PersonId, amount));
            debtor.Balance += amount;
            creditor.Balance -= amount;
        }

        return suggestions;
    }

    private sealed class Working
    {
        public Working(Guid personId, long seq, long balance)
        {
            PersonId = personId;
            Seq = seq;
            Balance = balance;
        }

        public Guid PersonId { get; }

        public long Seq { get; }

        public long Balance { get; set; }
    }
}
