namespace TripExpenseTracker.Core.Trips;

/// <summary>
/// A trip owned by one user (F2.R1). Name is trimmed and 1 to 100 characters (F2.R2). Times are UTC.
/// PeopleCount is filled only by the owner-filtered detail read (F3.R11, F2.R7); other reads leave it at 0.
/// </summary>
public sealed record Trip(Guid Id, Guid OwnerUserId, string Name, DateTimeOffset CreatedAt, int PeopleCount = 0);
