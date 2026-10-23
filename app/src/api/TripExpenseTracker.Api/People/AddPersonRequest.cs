namespace TripExpenseTracker.Api.People;

/// <summary>
/// Body of <c>POST /api/trips/{tripId}/people</c> (F3 Contract changes). The request carries only the name. The owner
/// and the usage flags are never read from the request (F3.R10, F3.AC8).
/// </summary>
public sealed record AddPersonRequest(string? Name);
