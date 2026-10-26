namespace TripExpenseTracker.Core.Expenses;

/// <summary>
/// Why an expense request was refused. The API maps each value to a status code and a ProblemDetails code.
/// The text and amount values mirror <c>MoneyFailure</c> and <c>ExpenseRuleFailure</c>, so one flat list serves the API.
/// </summary>
public enum ExpenseFailure
{
    /// <summary>Blank after trimming (F4.R2). Code <c>description_required</c>.</summary>
    DescriptionRequired,

    /// <summary>More than 100 code points (F4.R2). Code <c>description_too_long</c>.</summary>
    DescriptionTooLong,

    /// <summary>No amount entered (F4.R3). Code <c>amount_required</c>.</summary>
    AmountRequired,

    /// <summary>Not a decimal (F4.R3). Code <c>amount_invalid</c>.</summary>
    AmountInvalid,

    /// <summary>More than 2 decimal places (F4.R3). Code <c>amount_too_many_decimals</c>.</summary>
    AmountTooManyDecimals,

    /// <summary>Zero or negative (F4.R3). Code <c>amount_not_positive</c>.</summary>
    AmountNotPositive,

    /// <summary>Does not fit in a stored <c>long</c>, or the trip total would overflow (Risks). Code <c>amount_too_large</c>.</summary>
    AmountTooLarge,

    /// <summary>No payer chosen (F4.R4). Code <c>payer_required</c>.</summary>
    PayerRequired,

    /// <summary>The payer is not a person on this trip (F4.R4). Code <c>payer_not_on_trip</c>.</summary>
    PayerNotOnTrip,

    /// <summary>No participants chosen (F4.R5). Code <c>participants_required</c>.</summary>
    ParticipantsRequired,

    /// <summary>The same person listed twice (F4.R5). Code <c>participant_duplicate</c>.</summary>
    ParticipantDuplicate,

    /// <summary>A participant is not a person on this trip (F4.R5). Code <c>participant_not_on_trip</c>.</summary>
    ParticipantNotOnTrip,

    /// <summary>The trip has no people, so no expense can be added (F4.AC10). Code <c>no_people_on_trip</c>.</summary>
    NoPeopleOnTrip,

    /// <summary>A person was removed between the check and the save. Code <c>people_changed</c>.</summary>
    PeopleChanged,

    /// <summary>The trip is not the owner's or does not exist, or the expense is not on it (F4.R15). Code <c>not_found</c>.</summary>
    NotFound,
}

/// <summary>An expense as the API shows it: payer, participants with their share, and the time added (F4.R7, R8).</summary>
public sealed record ExpenseItem(
    Guid Id,
    string Description,
    long AmountCents,
    ExpensePerson Payer,
    IReadOnlyList<ExpenseParticipantShare> Participants,
    DateTimeOffset CreatedAt);

/// <summary>A participant's name and share in cents. A payer who is not a participant does not appear here (F4.R6).</summary>
public sealed record ExpenseParticipantShare(Guid PersonId, string Name, long ShareCents);

/// <summary>The saved expense, or the reason it was refused. Never both.</summary>
public sealed record ExpenseAddResult(ExpenseItem? Expense, ExpenseFailure? Failure)
{
    public bool Succeeded => Expense is not null;

    public static ExpenseAddResult Added(ExpenseItem expense) => new(expense, null);

    public static ExpenseAddResult Refused(ExpenseFailure failure) => new(null, failure);
}

/// <summary>The trip's expenses newest first with the total, or <see cref="ExpenseFailure.NotFound"/>. Never both.</summary>
public sealed record ExpenseListResult(long Total, int Count, IReadOnlyList<ExpenseItem> Expenses, ExpenseFailure? Failure)
{
    public bool Succeeded => Failure is null;

    public static ExpenseListResult Found(long total, IReadOnlyList<ExpenseItem> expenses) =>
        new(total, expenses.Count, expenses, null);

    public static ExpenseListResult Refused(ExpenseFailure failure) => new(0, 0, Array.Empty<ExpenseItem>(), failure);
}

/// <summary>A removal that happened, or <see cref="ExpenseFailure.NotFound"/>.</summary>
public sealed record ExpenseDeleteResult(ExpenseFailure? Failure)
{
    public bool Removed => Failure is null;

    public static ExpenseDeleteResult Done() => new(Failure: null);

    public static ExpenseDeleteResult Refused(ExpenseFailure failure) => new(Failure: failure);
}
