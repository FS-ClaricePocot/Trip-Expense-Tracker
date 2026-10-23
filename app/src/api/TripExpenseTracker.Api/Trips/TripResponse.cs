using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Api.Trips;

/// <summary>
/// A trip in the list, and the body of a created trip (F2 Contract changes). peopleCount is real in the list from F3 on
/// (F3.R13); a created trip has no people yet, so it is 0.
/// </summary>
public sealed record TripResponse(Guid Id, string Name, DateTimeOffset CreatedAt, int PeopleCount)
{
    public static TripResponse From(Trip trip) => new(trip.Id, trip.Name, trip.CreatedAt, trip.PeopleCount);
}
