namespace TripExpenseTracker.Core.Expenses;

/// <summary>
/// A saved expense (F4.R1). Amount is whole cents. Shares are not stored; they are computed from the amount and the
/// participants' order (see <see cref="ExpenseShares"/>). CreatedAt is UTC, from the clock (F4.R8).
/// </summary>
public sealed record Expense(Guid Id, Guid TripId, string Description, long AmountCents, Guid PayerPersonId, DateTimeOffset CreatedAt);

/// <summary>A person as an expense sees them: id, name, and their order on the trip (<c>people.seq</c>, F3.R5).</summary>
public sealed record ExpensePerson(Guid Id, string Name, long Seq);

/// <summary>A stored expense with its payer and participants. <see cref="Seq"/> is the list order (F4.R8).</summary>
public sealed record StoredExpense(Expense Expense, long Seq, ExpensePerson Payer, IReadOnlyList<ExpensePerson> Participants);
