using Dapper;
using Npgsql;
using TripExpenseTracker.Core.Expenses;

namespace TripExpenseTracker.Data;

/// <summary>
/// Dapper implementation of <see cref="IExpenseDao"/>. Every statement joins trips and filters on owner_user_id, so a
/// trip the user does not own looks the same as a missing one (F4.R15). Parameters only; no SQL is built from input.
/// Writes run on the unit of work's transaction (the service opens it), so an expense and its participants are
/// all or nothing.
/// </summary>
public sealed class ExpenseDao : IExpenseDao
{
    // Payer name and seq come from the same trip (composite key), so the join always finds the payer.
    private const string ExpensesSql = """
        SELECT e.id AS "Id", e.description AS "Description", e.amount_cents AS "AmountCents",
               e.created_at AS "CreatedAt", e.seq AS "Seq",
               p.id AS "PayerId", p.name AS "PayerName", p.seq AS "PayerSeq"
        FROM expenses e
        JOIN trips t ON t.id = e.trip_id
        JOIN people p ON p.id = e.payer_person_id AND p.trip_id = e.trip_id
        WHERE e.trip_id = @TripId AND t.owner_user_id = @Owner
        ORDER BY e.seq DESC
        """;

    // Participants with people.seq, so the service can compute the shares in the order added (F4.R7).
    private const string ParticipantsSql = """
        SELECT ep.expense_id AS "ExpenseId", p.id AS "Id", p.name AS "Name", p.seq AS "Seq"
        FROM expense_participants ep
        JOIN expenses e ON e.id = ep.expense_id AND e.trip_id = ep.trip_id
        JOIN trips t ON t.id = e.trip_id
        JOIN people p ON p.id = ep.person_id AND p.trip_id = ep.trip_id
        WHERE e.trip_id = @TripId AND t.owner_user_id = @Owner
        ORDER BY p.seq
        """;

    // Zero rows means the trip is not the owner's or does not exist (F4.R15).
    private const string InsertExpenseSql = """
        INSERT INTO expenses (id, trip_id, description, amount_cents, payer_person_id, created_at)
        SELECT CAST(@Id AS uuid), t.id, CAST(@Description AS text), CAST(@AmountCents AS bigint), CAST(@PayerPersonId AS uuid), CAST(@CreatedAt AS timestamptz)
        FROM trips t
        WHERE t.id = @TripId AND t.owner_user_id = @Owner
        """;

    // The expense must be on the owner's trip, so a participant can't be added to someone else's expense.
    private const string InsertParticipantSql = """
        INSERT INTO expense_participants (expense_id, trip_id, person_id)
        SELECT CAST(@ExpenseId AS uuid), t.id, CAST(@PersonId AS uuid)
        FROM trips t
        WHERE t.id = @TripId AND t.owner_user_id = @Owner
        """;

    // The owner join in the USING clause means an expense on another user's trip matches no row (F4.R15).
    // Participants are removed by the ON DELETE CASCADE on expense_participants.
    private const string DeleteSql = """
        DELETE FROM expenses e
        USING trips t
        WHERE e.id = @ExpenseId AND e.trip_id = t.id AND t.id = @TripId AND t.owner_user_id = @Owner
        """;

    private const string ForeignKeyViolation = "23503";

    private readonly IUnitOfWork _unitOfWork;

    public ExpenseDao(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<StoredExpense>> ListAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var parameters = new { Owner = owner, TripId = tripId };

        var expenseRows = await connection.QueryAsync<ExpenseRow>(new CommandDefinition(
            ExpensesSql,
            parameters,
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        var participantRows = await connection.QueryAsync<ParticipantRow>(new CommandDefinition(
            ParticipantsSql,
            parameters,
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        var participantsByExpense = participantRows
            .GroupBy(row => row.ExpenseId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ExpensePerson>)group.Select(row => new ExpensePerson(row.Id, row.Name, row.Seq)).ToList());

        return expenseRows
            .Select(row => new StoredExpense(
                new Expense(
                    row.Id,
                    tripId,
                    row.Description,
                    row.AmountCents,
                    row.PayerId,
                    ToUtc(row.CreatedAt)),
                row.Seq,
                new ExpensePerson(row.PayerId, row.PayerName, row.PayerSeq),
                participantsByExpense.TryGetValue(row.Id, out var participants)
                    ? participants
                    : Array.Empty<ExpensePerson>()))
            .ToList();
    }

    public async Task<ExpenseInsertOutcome> InsertAsync(Guid owner, Expense expense, IReadOnlyList<Guid> participantPersonIds, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);

        try
        {
            var inserted = await connection.ExecuteAsync(new CommandDefinition(
                InsertExpenseSql,
                new
                {
                    Owner = owner,
                    TripId = expense.TripId,
                    Id = expense.Id,
                    Description = expense.Description,
                    AmountCents = expense.AmountCents,
                    PayerPersonId = expense.PayerPersonId,
                    CreatedAt = expense.CreatedAt.UtcDateTime,
                },
                transaction: _unitOfWork.Transaction,
                cancellationToken: cancellationToken));

            if (inserted == 0)
            {
                return ExpenseInsertOutcome.TripNotFound;
            }

            foreach (var personId in participantPersonIds)
            {
                var rows = await connection.ExecuteAsync(new CommandDefinition(
                    InsertParticipantSql,
                    new { Owner = owner, TripId = expense.TripId, ExpenseId = expense.Id, PersonId = personId },
                    transaction: _unitOfWork.Transaction,
                    cancellationToken: cancellationToken));

                if (rows == 0)
                {
                    return ExpenseInsertOutcome.TripNotFound;
                }
            }

            return ExpenseInsertOutcome.Inserted;
        }
        catch (PostgresException ex) when (ex.SqlState == ForeignKeyViolation)
        {
            // The payer or a participant was removed after the service checked them. The composite keys refuse the row (F4.R4, R5).
            return ExpenseInsertOutcome.PersonMissing;
        }
    }

    public async Task<bool> DeleteAsync(Guid owner, Guid tripId, Guid expenseId, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            DeleteSql,
            new { Owner = owner, TripId = tripId, ExpenseId = expenseId },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.ExecuteAsync(command);
        return rows > 0;
    }

    private static DateTimeOffset ToUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    /// <summary>Reads a row with the types Npgsql returns. timestamptz comes back as DateTime, converted here (same as PersonDao).</summary>
    internal sealed record ExpenseRow(
        Guid Id,
        string Description,
        long AmountCents,
        DateTime CreatedAt,
        long Seq,
        Guid PayerId,
        string PayerName,
        long PayerSeq);

    internal sealed record ParticipantRow(Guid ExpenseId, Guid Id, string Name, long Seq);
}
