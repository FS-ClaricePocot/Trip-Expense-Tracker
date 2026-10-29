namespace TripExpenseTracker.Api.Payments;

/// <summary>
/// Body of <c>POST /api/trips/{tripId}/payments</c> (F6 Contract changes). The client sends the suggestion it saw, in
/// integer cents. The server checks it against the current suggestions (F6.R18). The owner is never read from the request.
/// </summary>
public sealed record RecordPaymentRequest(Guid PayerPersonId, Guid PayeePersonId, long AmountCents);
