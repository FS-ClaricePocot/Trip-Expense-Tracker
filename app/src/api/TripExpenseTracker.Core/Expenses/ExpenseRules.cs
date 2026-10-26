namespace TripExpenseTracker.Core.Expenses;

/// <summary>Why an expense's text or people were refused (F4.R2, R4, R5). The API maps each value to a code.</summary>
public enum ExpenseRuleFailure
{
    /// <summary>Blank after trimming (F4.R2). Code <c>description_required</c>.</summary>
    DescriptionRequired,

    /// <summary>More than 100 code points after trimming (F4.R2). Code <c>description_too_long</c>.</summary>
    DescriptionTooLong,

    /// <summary>No payer was given (F4.AC6). Code <c>payer_required</c>.</summary>
    PayerRequired,

    /// <summary>No participants were given (F4.R5). Code <c>participants_required</c>.</summary>
    ParticipantsRequired,

    /// <summary>The same person is listed twice as a participant. Code <c>participant_duplicate</c>.</summary>
    ParticipantDuplicate,
}

/// <summary>The trimmed description that would be saved, and the refusal if any rule failed.</summary>
public sealed record ExpenseCheck(string Description, ExpenseRuleFailure? Failure)
{
    public bool IsValid => Failure is null;
}

/// <summary>
/// Expense rules that need no I/O (F4.R2, F4.R5, F4.R6, F4.R7). Trims the description, refuses blank or over 100
/// code points, requires a payer and at least one participant, and refuses duplicate participants.
/// Whether the payer and participants are on the trip is checked by the service, not here. Pure logic.
/// </summary>
public static class ExpenseRules
{
    public const int MaxDescriptionLength = 100;

    public static ExpenseCheck Check(string? description, Guid? payerPersonId, IReadOnlyList<Guid>? participantIds)
    {
        var text = (description ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return new ExpenseCheck(text, ExpenseRuleFailure.DescriptionRequired);
        }

        if (CountCodePoints(text) > MaxDescriptionLength)
        {
            return new ExpenseCheck(text, ExpenseRuleFailure.DescriptionTooLong);
        }

        if (payerPersonId is null || payerPersonId.Value == Guid.Empty)
        {
            return new ExpenseCheck(text, ExpenseRuleFailure.PayerRequired);
        }

        if (participantIds is null || participantIds.Count == 0)
        {
            return new ExpenseCheck(text, ExpenseRuleFailure.ParticipantsRequired);
        }

        var seen = new HashSet<Guid>();
        foreach (var id in participantIds)
        {
            if (!seen.Add(id))
            {
                return new ExpenseCheck(text, ExpenseRuleFailure.ParticipantDuplicate);
            }
        }

        return new ExpenseCheck(text, null);
    }

    private static int CountCodePoints(string text)
    {
        var count = 0;
        var runes = text.EnumerateRunes();
        while (runes.MoveNext())
        {
            count++;
        }

        return count;
    }
}
