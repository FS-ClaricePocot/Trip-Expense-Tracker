namespace TripExpenseTracker.Api.Trips;

/// <summary>
/// Body of <c>POST /api/trips</c> (F2 Contract changes). <c>ConfirmDuplicate</c> is optional; only <c>true</c> confirms
/// a duplicate name (F2.R3). The owner is never read from the request (F2.R9).
/// </summary>
public sealed record CreateTripRequest(string? Name, bool? ConfirmDuplicate);
