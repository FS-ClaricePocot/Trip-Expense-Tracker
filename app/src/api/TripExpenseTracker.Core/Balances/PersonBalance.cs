namespace TripExpenseTracker.Core.Balances;

/// <summary>Whether a person is owed money, owes money, or is settled up (F5.R2). The sign is never shown.</summary>
public enum BalanceStatus
{
    Owed,
    Owes,
    Settled,
}

/// <summary>
/// A person's balance in cents (F5.R1, F5.R2). Positive means owed, negative means owes, zero means settled.
/// The status is derived from the sign.
/// </summary>
public sealed record PersonBalance(Guid PersonId, string Name, long BalanceCents)
{
    public BalanceStatus Status => BalanceCents switch
    {
        > 0 => BalanceStatus.Owed,
        < 0 => BalanceStatus.Owes,
        _ => BalanceStatus.Settled,
    };
}
