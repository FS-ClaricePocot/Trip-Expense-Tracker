using Dapper;
using TripExpenseTracker.Core.Balances;

namespace TripExpenseTracker.Data;

/// <summary>
/// Dapper implementation of <see cref="IBalanceDao"/>. Every statement joins trips and filters on owner_user_id, so a
/// trip the user does not own looks the same as a missing one (F5.R12). Parameters only; no SQL is built from input.
/// Read-only: balances are never written or edited (F5.R11).
/// </summary>
public sealed class BalanceDao : IBalanceDao
{
    // Ordered by seq, the order the balance rows are listed in (F5.R4, F3.R5).
    private const string PeopleSql = """
        SELECT p.id AS "Id", p.name AS "Name", p.seq AS "Seq"
        FROM people p
        JOIN trips t ON t.id = p.trip_id
        WHERE p.trip_id = @TripId AND t.owner_user_id = @Owner
        ORDER BY p.seq
        """;

    private const string ExpensesSql = """
        SELECT e.id AS "Id", e.amount_cents AS "AmountCents", e.payer_person_id AS "PayerId"
        FROM expenses e
        JOIN trips t ON t.id = e.trip_id
        WHERE e.trip_id = @TripId AND t.owner_user_id = @Owner
        """;

    // Same ordering as F4's participant list (people.seq), so the share rule sees the participants in the order added.
    private const string ParticipantsSql = """
        SELECT ep.expense_id AS "ExpenseId", p.id AS "PersonId", p.seq AS "Seq"
        FROM expense_participants ep
        JOIN expenses e ON e.id = ep.expense_id AND e.trip_id = ep.trip_id
        JOIN trips t ON t.id = e.trip_id
        JOIN people p ON p.id = ep.person_id AND p.trip_id = ep.trip_id
        WHERE e.trip_id = @TripId AND t.owner_user_id = @Owner
        ORDER BY p.seq
        """;

    private readonly IUnitOfWork _unitOfWork;

    public BalanceDao(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<BalanceSnapshot> GetSnapshotAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var parameters = new { Owner = owner, TripId = tripId };

        var peopleRows = await connection.QueryAsync<PersonRow>(new CommandDefinition(
            PeopleSql,
            parameters,
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken));

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
                group => (IReadOnlyList<(Guid PersonId, long Seq)>)group.Select(row => (row.PersonId, row.Seq)).ToList());

        var people = peopleRows
            .Select(row => new BalancePerson(row.Id, row.Name, row.Seq))
            .ToList();

        var expenses = expenseRows
            .Select(row => new BalanceExpense(
                row.AmountCents,
                row.PayerId,
                participantsByExpense.TryGetValue(row.Id, out var participants)
                    ? participants
                    : Array.Empty<(Guid PersonId, long Seq)>()))
            .ToList();

        return new BalanceSnapshot(people, expenses);
    }

    /// <summary>Row types as Npgsql returns them. Column aliases match the property names.</summary>
    internal sealed record PersonRow(Guid Id, string Name, long Seq);

    internal sealed record ExpenseRow(Guid Id, long AmountCents, Guid PayerId);

    internal sealed record ParticipantRow(Guid ExpenseId, Guid PersonId, long Seq);
}
