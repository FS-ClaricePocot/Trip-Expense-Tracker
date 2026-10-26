namespace TripExpenseTracker.Core.People;

/// <summary>
/// Person persistence. Every method takes the owner, and the implementation must filter on it (the owner join in
/// the WHERE clause), so a trip the user does not own looks the same as a missing one (F3.R10). Methods run on the
/// unit of work of the surrounding transaction when there is one.
/// </summary>
public interface IPersonDao
{
    /// <summary>The trip's people in the order added (F3.R5). Empty when the trip is not the owner's.</summary>
    Task<IReadOnlyList<Person>> ListAsync(Guid owner, Guid tripId, CancellationToken cancellationToken);

    /// <summary>Adds the person to the owner's trip. Reports a duplicate name or a missing trip (F3.R3, F3.R10).</summary>
    Task<PersonAddOutcome> AddAsync(Guid owner, Guid tripId, Person person, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the person from the owner's trip. Reports <see cref="PersonDeleteOutcome.InUse"/> when the database
    /// refuses the delete because an expense or payment still refers to the person (F3.R7).
    /// </summary>
    Task<PersonDeleteOutcome> DeleteAsync(Guid owner, Guid tripId, Guid personId, CancellationToken cancellationToken);
}
