namespace TripExpenseTracker.Core.People;

/// <summary>
/// A person on one trip (F3.R1). A person has no account, so only a name and the time they were added are kept.
/// Name is trimmed and 1 to 100 characters (F3.R2). AddedAt is UTC.
/// </summary>
public sealed record Person(Guid Id, Guid TripId, string Name, DateTimeOffset AddedAt);

/// <summary>A person with the usage flags that drive the "In expenses" and "In payments" tags (F3.R7).</summary>
public sealed record PersonWithUsage(Person Person, bool InExpense, bool InPayment);
