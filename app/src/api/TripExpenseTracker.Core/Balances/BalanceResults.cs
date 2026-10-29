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

/// <summary>
/// The balances in <c>seq</c> order, or the reason they were refused. Never both. <c>PeopleCount</c> is set only for
/// <see cref="BalanceServiceFailure.BalancesInconsistent"/>, so the API can log the trip id and person count (F5.AC16).
/// </summary>
public sealed record BalanceListResult(IReadOnlyList<PersonBalance>? Balances, BalanceServiceFailure? Failure, int? PeopleCount = null)
{
    public bool Succeeded => Balances is not null;

    public static BalanceListResult Found(IReadOnlyList<PersonBalance> balances) => new(balances, null);

    public static BalanceListResult Refused(BalanceServiceFailure failure, int? peopleCount = null) => new(null, failure, peopleCount);
}

/// <summary>The trip page tiles, or the reason they were refused
/// (<see cref="BalanceServiceFailure.NotFound"/> only). Never both. SuggestionCount is the number of suggested payments
/// behind Left to settle ("in {n} payments", F5.R8, F6.R2); it is 0 when refused.</summary>
public sealed record BalanceSummaryResult(BalanceSummary? Summary, BalanceServiceFailure? Failure, int SuggestionCount = 0)
{
    public bool Succeeded => Summary is not null;

    public static BalanceSummaryResult Found(BalanceSummary summary, int suggestionCount = 0) => new(summary, null, suggestionCount);

    public static BalanceSummaryResult Refused(BalanceServiceFailure failure) => new(null, failure);
}
