using TripExpenseTracker.Core.Expenses;

namespace TripExpenseTracker.Api.Expenses;

/// <summary>
/// Body of <c>GET /api/trips/{tripId}/expenses</c> (F4 Contract changes). The list is returned whole, newest first, with
/// the total of every amount in integer cents (F4.R13, F4.R16).
/// </summary>
public sealed record ExpenseListResponse(long Total, int Count, IReadOnlyList<ExpenseResponse> Expenses)
{
    public static ExpenseListResponse From(ExpenseListResult result) => new(
        result.Total,
        result.Count,
        result.Expenses.Select(ExpenseResponse.From).ToList());
}
