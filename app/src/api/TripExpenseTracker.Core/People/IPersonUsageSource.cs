namespace TripExpenseTracker.Core.People;

/// <summary>Which kind of record a usage source reports on (F3.R7).</summary>
public enum PersonUsageKind
{
    Expense,
    Payment,
}

/// <summary>
/// Extension point for the in-use check (F3.R7). F4 (expenses) and F6 (payments) each register one. F3 registers
/// none, so nobody is in use until they do. Each implementation must filter on <c>trip_id</c>. The service only
/// calls a source after the trip is confirmed to belong to the owner.
/// </summary>
public interface IPersonUsageSource
{
    PersonUsageKind Kind { get; }

    /// <summary>The ids of people on this trip that the source's records refer to.</summary>
    Task<IReadOnlyCollection<Guid>> GetPersonIdsInUseAsync(Guid tripId, CancellationToken cancellationToken);
}
