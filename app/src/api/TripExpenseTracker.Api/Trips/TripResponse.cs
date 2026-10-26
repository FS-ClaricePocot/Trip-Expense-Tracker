using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Api.Trips;

/// <summary>A trip in the list, and the body of a created trip (F2 Contract changes).</summary>
public sealed record TripResponse(Guid Id, string Name, DateTimeOffset CreatedAt)
{
    public static TripResponse From(Trip trip) => new(trip.Id, trip.Name, trip.CreatedAt);
}
