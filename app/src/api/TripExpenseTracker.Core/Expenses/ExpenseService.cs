using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.Money;
using TripExpenseTracker.Core.People;
using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Core.Expenses;

/// <summary>
/// Expense add, list and delete rules (F4.R1, R4 to R9, R12 to R15). Text and amount are checked with the pure
/// rules first, then the trip, its people and the trip total are checked inside one transaction, so the insert of
/// the expense and its participants is all or nothing (architecture rule: transactions). Every call is for one owner,
/// and the trip must belong to the owner before any expense is read or changed (F4.R15).
/// </summary>
public sealed class ExpenseService
{
    private readonly IExpenseDao _expenses;
    private readonly IPersonDao _people;
    private readonly ITripDao _trips;
    private readonly IAuthTransaction _transaction;
    private readonly TimeProvider _time;

    public ExpenseService(
        IExpenseDao expenses,
        IPersonDao people,
        ITripDao trips,
        IAuthTransaction transaction,
        TimeProvider time)
    {
        _expenses = expenses;
        _people = people;
        _trips = trips;
        _transaction = transaction;
        _time = time;
    }

    /// <summary>
    /// Checks the text, amount, payer and participants, then the trip and its people, then the trip total, and saves
    /// the expense with its participants in one transaction (F4.R1, R4 to R6, F4.AC10).
    /// </summary>
    public async Task<ExpenseAddResult> AddAsync(
        Guid owner,
        Guid tripId,
        string? description,
        string? amountText,
        Guid? payerPersonId,
        IReadOnlyList<Guid>? participantPersonIds,
        CancellationToken cancellationToken)
    {
        var check = ExpenseRules.Check(description, payerPersonId, participantPersonIds);
        if (!check.IsValid)
        {
            return ExpenseAddResult.Refused(MapRule(check.Failure!.Value));
        }

        var money = MoneyParser.ParseCents(amountText);
        if (!money.Succeeded)
        {
            return ExpenseAddResult.Refused(MapMoney(money.Failure!.Value));
        }

        // ExpenseRules guarantees a payer and at least one participant, so ExpenseShares will not throw later.
        var cents = money.Cents!.Value;
        var payer = payerPersonId!.Value;
        var participants = participantPersonIds!;

        try
        {
            return await _transaction.RunAsync<ExpenseAddResult>(async token =>
            {
                if (await _trips.GetAsync(owner, tripId, token) is null)
                {
                    return ExpenseAddResult.Refused(ExpenseFailure.NotFound);
                }

                var people = await _people.ListAsync(owner, tripId, token);
                if (people.Count == 0)
                {
                    return ExpenseAddResult.Refused(ExpenseFailure.NoPeopleOnTrip);
                }

                // The list is in the order added (F3.R5), so the index is the person's seq for the leftover-cent rule.
                var byId = people
                    .Select((person, index) => new ExpensePerson(person.Id, person.Name, index))
                    .ToDictionary(person => person.Id);

                if (!byId.ContainsKey(payer))
                {
                    return ExpenseAddResult.Refused(ExpenseFailure.PayerNotOnTrip);
                }

                if (participants.Any(id => !byId.ContainsKey(id)))
                {
                    return ExpenseAddResult.Refused(ExpenseFailure.ParticipantNotOnTrip);
                }

                var existing = await _expenses.ListAsync(owner, tripId, token);
                if (!TrySum(existing.Select(e => e.Expense.AmountCents).Append(cents), out _))
                {
                    return ExpenseAddResult.Refused(ExpenseFailure.AmountTooLarge);
                }

                var expense = new Expense(Guid.NewGuid(), tripId, check.Description, cents, payer, _time.GetUtcNow());
                var outcome = await _expenses.InsertAsync(owner, expense, participants, token);
                if (outcome != ExpenseInsertOutcome.Inserted)
                {
                    // Throwing rolls back the transaction, so a partly written expense is not kept.
                    throw new InsertRefusedException(outcome == ExpenseInsertOutcome.TripNotFound
                        ? ExpenseFailure.NotFound
                        : ExpenseFailure.PeopleChanged);
                }

                var item = BuildItem(
                    expense.Id,
                    expense.Description,
                    cents,
                    byId[payer],
                    expense.CreatedAt,
                    participants.Select(id => byId[id]).ToList());
                return ExpenseAddResult.Added(item);
            }, cancellationToken);
        }
        catch (InsertRefusedException refused)
        {
            return ExpenseAddResult.Refused(refused.Failure);
        }
    }

    /// <summary>The trip's expenses, newest first, with the total of every amount (F4.R8, R13, R15).</summary>
    public async Task<ExpenseListResult> ListAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
    {
        if (await _trips.GetAsync(owner, tripId, cancellationToken) is null)
        {
            return ExpenseListResult.Refused(ExpenseFailure.NotFound);
        }

        var stored = await _expenses.ListAsync(owner, tripId, cancellationToken);
        var items = stored
            .OrderByDescending(e => e.Seq)
            .Select(e => BuildItem(e.Expense.Id, e.Expense.Description, e.Expense.AmountCents, e.Payer, e.Expense.CreatedAt, e.Participants))
            .ToList();
        var total = Sum(stored.Select(e => e.Expense.AmountCents));

        return ExpenseListResult.Found(total, items);
    }

    /// <summary>Deletes one expense on the owner's trip. Anything else is not found (F4.R9, R15).</summary>
    public async Task<ExpenseDeleteResult> DeleteAsync(Guid owner, Guid tripId, Guid expenseId, CancellationToken cancellationToken)
    {
        return await _expenses.DeleteAsync(owner, tripId, expenseId, cancellationToken)
            ? ExpenseDeleteResult.Done()
            : ExpenseDeleteResult.Refused(ExpenseFailure.NotFound);
    }

    /// <summary>Builds the item with each participant's share in seq order (F4.R7). Shares are never stored.</summary>
    private static ExpenseItem BuildItem(
        Guid id,
        string description,
        long amountCents,
        ExpensePerson payer,
        DateTimeOffset createdAt,
        IReadOnlyList<ExpensePerson> participants)
    {
        if (participants.Count == 0)
        {
            return new ExpenseItem(id, description, amountCents, payer, Array.Empty<ExpenseParticipantShare>(), createdAt);
        }

        var names = participants.ToDictionary(p => p.Id, p => p.Name);
        var shares = ExpenseShares.Compute(
            amountCents,
            participants.Select(p => (PersonId: p.Id, Seq: p.Seq)).ToList());
        var rows = shares
            .Select(share => new ExpenseParticipantShare(share.PersonId, names[share.PersonId], share.Cents))
            .ToList();

        return new ExpenseItem(id, description, amountCents, payer, rows, createdAt);
    }

    /// <summary>Adds with checked arithmetic and reports overflow instead of throwing (Risks: amount_too_large).</summary>
    private static bool TrySum(IEnumerable<long> amounts, out long total)
    {
        total = 0;
        try
        {
            foreach (var amount in amounts)
            {
                total = checked(total + amount);
            }

            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    /// <summary>Sums with checked arithmetic (F4.R13).</summary>
    private static long Sum(IEnumerable<long> amounts)
    {
        long total = 0;
        foreach (var amount in amounts)
        {
            total = checked(total + amount);
        }

        return total;
    }

    private static ExpenseFailure MapRule(ExpenseRuleFailure failure) => failure switch
    {
        ExpenseRuleFailure.DescriptionRequired => ExpenseFailure.DescriptionRequired,
        ExpenseRuleFailure.DescriptionTooLong => ExpenseFailure.DescriptionTooLong,
        ExpenseRuleFailure.PayerRequired => ExpenseFailure.PayerRequired,
        ExpenseRuleFailure.ParticipantsRequired => ExpenseFailure.ParticipantsRequired,
        ExpenseRuleFailure.ParticipantDuplicate => ExpenseFailure.ParticipantDuplicate,
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null),
    };

    private static ExpenseFailure MapMoney(MoneyFailure failure) => failure switch
    {
        MoneyFailure.AmountRequired => ExpenseFailure.AmountRequired,
        MoneyFailure.AmountInvalid => ExpenseFailure.AmountInvalid,
        MoneyFailure.AmountTooManyDecimals => ExpenseFailure.AmountTooManyDecimals,
        MoneyFailure.AmountNotPositive => ExpenseFailure.AmountNotPositive,
        MoneyFailure.AmountTooLarge => ExpenseFailure.AmountTooLarge,
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null),
    };

    /// <summary>Carries a refusal out of the transaction so it rolls back. Never leaves the service.</summary>
    private sealed class InsertRefusedException(ExpenseFailure failure) : Exception
    {
        public ExpenseFailure Failure { get; } = failure;
    }
}
