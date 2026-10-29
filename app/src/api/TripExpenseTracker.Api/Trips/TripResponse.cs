using TripExpenseTracker.Core.Balances;
using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Api.Trips;

/// <summary>
/// A trip in the list: the trip fields and <c>settle</c> (F6 Contract changes, F6.R19). The existing fields keep their
/// names and order, so the change is additive. The create response is still <see cref="TripResponse"/>, without settle.
/// </summary>
public sealed record TripListItemResponse(Guid Id, string Name, DateTimeOffset CreatedAt, int PeopleCount, int ExpenseCount, long TotalCents, SettleResponse Settle)
    : TripResponse(Id, Name, CreatedAt, PeopleCount, ExpenseCount, TotalCents)
{
    public static TripListItemResponse From(Trip trip, TripStatus status) =>
        new(trip.Id, trip.Name, trip.CreatedAt, trip.PeopleCount, trip.ExpenseCount, trip.TotalCents, SettleResponse.From(status));
}

/// <summary>The card's settle status: "none", "toSettle" or "settled", and the payments still to make (F6.R19).</summary>
public sealed record SettleResponse(string Status, int PaymentsToSettle)
{
    public static SettleResponse From(TripStatus status) => new(
        status.State switch
        {
            SettleState.ToSettle => "toSettle",
            SettleState.Settled => "settled",
            _ => "none",
        },
        status.PaymentsToSettle);
}

/// <summary>
/// A trip in the list, and the body of a created trip (F2 Contract changes). peopleCount is real in the list from F3 on
/// (F3.R13); a created trip has no people yet, so it is 0. expenseCount and totalCents are real in the list from F4 on
/// (F4.R17, F4.R18); a created trip has no expenses, so both are 0.
/// </summary>
public record TripResponse(Guid Id, string Name, DateTimeOffset CreatedAt, int PeopleCount, int ExpenseCount, long TotalCents)
{
    public static TripResponse From(Trip trip) => new(trip.Id, trip.Name, trip.CreatedAt, trip.PeopleCount, trip.ExpenseCount, trip.TotalCents);
}
