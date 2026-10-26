using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Api.Trips;

/// <summary>
/// Body of <c>GET /api/trips/{id}</c>. peopleCount is real from F3 on, and expenseCount and totalCents are real from F4 on
/// (F4.R13). paymentCount stays 0 until F6 makes it real in its own plan (F2.R7).
/// </summary>
public sealed record TripDetailResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    int PeopleCount,
    int ExpenseCount,
    long TotalCents,
    int PaymentCount)
{
    public static TripDetailResponse From(Trip trip) => new(trip.Id, trip.Name, trip.CreatedAt, trip.PeopleCount, trip.ExpenseCount, trip.TotalCents, 0);
}
