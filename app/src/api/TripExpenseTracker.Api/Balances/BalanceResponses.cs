using TripExpenseTracker.Core.Balances;

namespace TripExpenseTracker.Api.Balances;

/// <summary>One person's balance in the list (F5 Contract changes). Status is one of "owed", "owes", "settled"; the sign is never shown (F5.R2).</summary>
public sealed record BalancePersonResponse(Guid Id, string Name, long BalanceCents, string Status)
{
    public static BalancePersonResponse From(PersonBalance balance) =>
        new(balance.PersonId, balance.Name, balance.BalanceCents, StatusText(balance.Status));

    private static string StatusText(BalanceStatus status) => status switch
    {
        BalanceStatus.Owed => "owed",
        BalanceStatus.Owes => "owes",
        BalanceStatus.Settled => "settled",
        _ => throw new InvalidOperationException("Unknown balance status."),
    };
}

/// <summary>The balances in <c>seq</c> order, with the total (always 0 when the zero-sum check passed) (F5.R3, F5.R4).</summary>
public sealed record BalanceListResponse(long TotalCents, IReadOnlyList<BalancePersonResponse> People)
{
    public static BalanceListResponse From(IReadOnlyList<PersonBalance> balances) =>
        new(balances.Sum(b => b.BalanceCents), balances.Select(BalancePersonResponse.From).ToList());
}

/// <summary>The trip page tiles (F5.R8). Integer cents only.</summary>
public sealed record BalanceSummaryResponse(long TotalSpentCents, long LeftToSettleCents)
{
    public static BalanceSummaryResponse From(BalanceSummary summary) =>
        new(summary.TotalSpentCents, summary.LeftToSettleCents);
}
