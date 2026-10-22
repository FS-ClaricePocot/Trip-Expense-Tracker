namespace TripExpenseTracker.Core.Trips;

/// <summary>A trip owned by one user (F2.R1). Name is trimmed and 1 to 100 characters (F2.R2). Times are UTC.</summary>
public sealed record Trip(Guid Id, Guid OwnerUserId, string Name, DateTimeOffset CreatedAt);
