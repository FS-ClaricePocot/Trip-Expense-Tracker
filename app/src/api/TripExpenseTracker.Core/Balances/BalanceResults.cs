namespace TripExpenseTracker.Core.Balances;

/// <summary>
/// Why a balance request was refused. <see cref="NotFound"/> covers a foreign, missing or non-GUID trip, so they look
/// the same (F5.R12). <see cref="BalancesInconsistent"/> is the zero-sum failure (F5.R3). The API maps each value.
/// </summary>
public enum BalanceServiceFailure
{
    NotFound,
    BalancesInconsistent,
}

/// <summary>The balances in <c>seq</c> order, or the reason they were refused. Never both.</summary>
public sealed record BalanceListResult(IReadOnlyList<PersonBalance>? Balances, BalanceServiceFailure? Failure)
{
    public bool Succeeded => Balances is not null;

    public static BalanceListResult Found(IReadOnlyList<PersonBalance> balances) => new(balances, null);

    public static BalanceListResult Refused(BalanceServiceFailure failure) => new(null, failure);
}

/// <summary>The trip page tiles, or the reason they were refused
/// (<see cref="BalanceServiceFailure.NotFound"/> only). Never both.</summary>
public sealed record BalanceSummaryResult(BalanceSummary? Summary, BalanceServiceFailure? Failure)
{
    public bool Succeeded => Summary is not null;

    public static BalanceSummaryResult Found(BalanceSummary summary) => new(summary, null);

    public static BalanceSummaryResult Refused(BalanceServiceFailure failure) => new(null, failure);
}
