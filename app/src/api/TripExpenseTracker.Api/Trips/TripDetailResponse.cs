using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Api.Trips;

/// <summary>
/// Body of <c>GET /api/trips/{id}</c>. The three counts are always 0 in F2, because people, expenses and payments
/// do not exist yet. F3, F4 and F6 make their counts real in their own plans (F2.R7).
/// </summary>
public sealed record TripDetailResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    int PeopleCount,
    int ExpenseCount,
    int PaymentCount)
{
    public static TripDetailResponse From(Trip trip) => new(trip.Id, trip.Name, trip.CreatedAt, 0, 0, 0);
}
