namespace TripExpenseTracker.Api.Expenses;

/// <summary>
/// Body of <c>POST /api/trips/{tripId}/expenses</c> (F4 Contract changes). <c>Amount</c> is decimal text and is parsed
/// only by <c>MoneyParser</c> in Core. The owner and any share are never read from the request (F4.R15, F4.R7).
/// </summary>
public sealed record CreateExpenseRequest(
    string? Description,
    string? Amount,
    Guid? PayerPersonId,
    IReadOnlyList<Guid>? ParticipantPersonIds);
