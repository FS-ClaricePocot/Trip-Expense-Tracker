namespace TripExpenseTracker.Core.Expenses;

/// <summary>What the DAO reports for an insert. A person removed after the service checked them is <see cref="PersonMissing"/> (F4.R4, R5).</summary>
public enum ExpenseInsertOutcome
{
    Inserted,

    /// <summary>The payer or a participant is no longer on the trip (the composite foreign key refused the row).</summary>
    PersonMissing,

    /// <summary>No trip matches the owner and trip id.</summary>
    TripNotFound,
}

/// <summary>
/// Expense persistence. Every method takes the owner, and the implementation must filter on it (the owner join in
/// the WHERE clause), so a trip the user does not own looks the same as a missing one (F4.R15). Methods run on the
/// unit of work of the surrounding transaction when there is one. Insert writes the expense and its participants
/// together; the caller runs it inside a transaction.
/// </summary>
public interface IExpenseDao
{
    /// <summary>The trip's expenses with payer and participants, in any order. Empty when the trip is not the owner's.</summary>
    Task<IReadOnlyList<StoredExpense>> ListAsync(Guid owner, Guid tripId, CancellationToken cancellationToken);

    /// <summary>Adds the expense and one row per participant to the owner's trip.</summary>
    Task<ExpenseInsertOutcome> InsertAsync(Guid owner, Expense expense, IReadOnlyList<Guid> participantPersonIds, CancellationToken cancellationToken);

    /// <summary>Deletes the expense if it is on the owner's trip (F4.R9, R15). Returns false when no row was removed.</summary>
    Task<bool> DeleteAsync(Guid owner, Guid tripId, Guid expenseId, CancellationToken cancellationToken);
}
