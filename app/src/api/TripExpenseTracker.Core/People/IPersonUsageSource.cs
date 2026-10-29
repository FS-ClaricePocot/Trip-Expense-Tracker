namespace TripExpenseTracker.Core.People;

/// <summary>Which kind of record a usage source reports on (F3.R7).</summary>
public enum PersonUsageKind
{
    Expense,
    Payment,
}

/// <summary>
/// Extension point for the in-use check (F3.R7). F4 (expenses) and F6 (payments) each register one. F3 registers
/// none, so nobody is in use until they do. Each implementation must join <c>trips</c> and filter on the trip and
/// its <c>owner_user_id</c>, like every other trip-scoped query, so another user's trip returns nobody.
/// </summary>
public interface IPersonUsageSource
{
    PersonUsageKind Kind { get; }

    /// <summary>The ids of people on this trip that the source's records refer to.</summary>
    Task<IReadOnlyCollection<Guid>> GetPersonIdsInUseAsync(Guid owner, Guid tripId, CancellationToken cancellationToken);
}
