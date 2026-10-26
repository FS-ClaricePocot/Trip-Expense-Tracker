using TripExpenseTracker.Core.Expenses;

namespace TripExpenseTracker.Api.Expenses;

/// <summary>
/// One expense as the API shows it (F4 Contract changes). Amounts are integer cents. There is no icon field (F4.R11),
/// and shares appear only inside <c>Participants</c>, never as a per-person field on the row.
/// </summary>
public sealed record ExpenseResponse(
    Guid Id,
    string Description,
    long AmountCents,
    ExpensePersonResponse Payer,
    int ParticipantCount,
    IReadOnlyList<ExpenseParticipantResponse> Participants,
    DateTimeOffset CreatedAt)
{
    public static ExpenseResponse From(ExpenseItem item) => new(
        item.Id,
        item.Description,
        item.AmountCents,
        new ExpensePersonResponse(item.Payer.Id, item.Payer.Name),
        item.Participants.Count,
        item.Participants
            .Select(p => new ExpenseParticipantResponse(p.PersonId, p.Name, p.ShareCents))
            .ToList(),
        item.CreatedAt);
}

/// <summary>A person's id and name, as the payer is shown.</summary>
public sealed record ExpensePersonResponse(Guid Id, string Name);

/// <summary>A participant's id, name and share in integer cents (F4.R7).</summary>
public sealed record ExpenseParticipantResponse(Guid Id, string Name, long ShareCents);
