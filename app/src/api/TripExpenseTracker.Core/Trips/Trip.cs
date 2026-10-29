namespace TripExpenseTracker.Core.Trips;

/// <summary>
/// A trip owned by one user (F2.R1). Name is trimmed and 1 to 100 characters (F2.R2). Times are UTC.
/// PeopleCount is filled only by the owner-filtered detail read (F3.R11, F2.R7); other reads leave it at 0.
/// ExpenseCount and TotalCents (F4.R13) are filled only by the detail read too; other reads leave them at 0.
/// PaymentCount (F2.R7, F6) is the number of recorded payments; it is filled by the same reads.
/// </summary>
public sealed record Trip(Guid Id, Guid OwnerUserId, string Name, DateTimeOffset CreatedAt, int PeopleCount = 0, int ExpenseCount = 0, long TotalCents = 0, int PaymentCount = 0);
