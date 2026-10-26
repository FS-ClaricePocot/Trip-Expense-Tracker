using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Api.Trips;

/// <summary>
/// A trip in the list, and the body of a created trip (F2 Contract changes). peopleCount is real in the list from F3 on
/// (F3.R13); a created trip has no people yet, so it is 0. expenseCount and totalCents are real in the list from F4 on
/// (F4.R17, F4.R18); a created trip has no expenses, so both are 0.
/// </summary>
public sealed record TripResponse(Guid Id, string Name, DateTimeOffset CreatedAt, int PeopleCount, int ExpenseCount, long TotalCents)
{
    public static TripResponse From(Trip trip) => new(trip.Id, trip.Name, trip.CreatedAt, trip.PeopleCount, trip.ExpenseCount, trip.TotalCents);
}
