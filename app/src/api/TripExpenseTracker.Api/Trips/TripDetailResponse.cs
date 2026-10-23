using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Api.Trips;

/// <summary>
/// Body of <c>GET /api/trips/{id}</c>. peopleCount is real from F3 on. expenseCount and paymentCount stay 0
/// until F4 and F6 make them real in their own plans (F2.R7).
/// </summary>
public sealed record TripDetailResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    int PeopleCount,
    int ExpenseCount,
    int PaymentCount)
{
    public static TripDetailResponse From(Trip trip) => new(trip.Id, trip.Name, trip.CreatedAt, trip.PeopleCount, 0, 0);
}
