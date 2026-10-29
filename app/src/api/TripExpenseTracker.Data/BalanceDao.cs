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

    // Payments as balances use them: the payer and payee and the amount (F5.R1, F6.R10). Owner-filtered like the rest.
    private const string PaymentsSql = """
        SELECT pm.payer_person_id AS "FromPersonId", pm.payee_person_id AS "ToPersonId", pm.amount_cents AS "AmountCents"
        FROM payments pm
        JOIN trips t ON t.id = pm.trip_id
        WHERE pm.trip_id = @TripId AND t.owner_user_id = @Owner
        """;

    // Owner-wide versions for the trip list (F6.R19). Each joins trips and filters on the owner; trip id is returned for grouping.
    private const string OwnerPeopleSql = """
        SELECT p.trip_id AS "TripId", p.id AS "Id", p.name AS "Name", p.seq AS "Seq"
        FROM people p
        JOIN trips t ON t.id = p.trip_id
        WHERE t.owner_user_id = @Owner
        ORDER BY p.seq
        """;

    private const string OwnerExpensesSql = """
        SELECT e.trip_id AS "TripId", e.id AS "Id", e.amount_cents AS "AmountCents", e.payer_person_id AS "PayerId"
        FROM expenses e
        JOIN trips t ON t.id = e.trip_id
        WHERE t.owner_user_id = @Owner
        """;

    private const string OwnerParticipantsSql = """
        SELECT ep.expense_id AS "ExpenseId", p.id AS "PersonId", p.seq AS "Seq"
        FROM expense_participants ep
        JOIN expenses e ON e.id = ep.expense_id AND e.trip_id = ep.trip_id
        JOIN trips t ON t.id = e.trip_id
        JOIN people p ON p.id = ep.person_id AND p.trip_id = ep.trip_id
        WHERE t.owner_user_id = @Owner
        ORDER BY p.seq
        """;

    private const string OwnerPaymentsSql = """
        SELECT pm.trip_id AS "TripId", pm.payer_person_id AS "FromPersonId", pm.payee_person_id AS "ToPersonId", pm.amount_cents AS "AmountCents"
        FROM payments pm
        JOIN trips t ON t.id = pm.trip_id
        WHERE t.owner_user_id = @Owner
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

        var paymentRows = await connection.QueryAsync<PaymentRow>(new CommandDefinition(
            PaymentsSql,
            parameters,
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        var payments = paymentRows
            .Select(row => new BalancePayment(row.FromPersonId, row.ToPersonId, row.AmountCents))
            .ToList();

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

        return new BalanceSnapshot(people, expenses, payments);
    }

    /// <summary>
    /// Every trip the owner has, in one read per table, each filtered on the owner through trips (F6.R19, F6.AC21).
    /// The rows are grouped by trip id in memory, so the trip list does not run one query per card.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, BalanceSnapshot>> ListForOwnerAsync(Guid owner, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var parameters = new { Owner = owner };

        var peopleRows = await connection.QueryAsync<OwnerPersonRow>(new CommandDefinition(
            OwnerPeopleSql,
            parameters,
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        var expenseRows = await connection.QueryAsync<OwnerExpenseRow>(new CommandDefinition(
            OwnerExpensesSql,
            parameters,
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        var participantRows = await connection.QueryAsync<ParticipantRow>(new CommandDefinition(
            OwnerParticipantsSql,
            parameters,
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        var paymentRows = await connection.QueryAsync<OwnerPaymentRow>(new CommandDefinition(
            OwnerPaymentsSql,
            parameters,
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken));

        var participantsByExpense = participantRows
            .GroupBy(row => row.ExpenseId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<(Guid PersonId, long Seq)>)group.Select(row => (row.PersonId, row.Seq)).ToList());

        var people = peopleRows
            .GroupBy(row => row.TripId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<BalancePerson>)group.Select(row => new BalancePerson(row.Id, row.Name, row.Seq)).ToList());

        var expenses = expenseRows
            .GroupBy(row => row.TripId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<BalanceExpense>)group
                    .Select(row => new BalanceExpense(
                        row.AmountCents,
                        row.PayerId,
                        participantsByExpense.TryGetValue(row.Id, out var participants)
                            ? participants
                            : Array.Empty<(Guid PersonId, long Seq)>()))
                    .ToList());

        var payments = paymentRows
            .GroupBy(row => row.TripId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<BalancePayment>)group
                    .Select(row => new BalancePayment(row.FromPersonId, row.ToPersonId, row.AmountCents))
                    .ToList());

        var tripIds = people.Keys.Concat(expenses.Keys).Concat(payments.Keys).Distinct();
        return tripIds.ToDictionary(
            tripId => tripId,
            tripId => new BalanceSnapshot(
                people.GetValueOrDefault(tripId) ?? Array.Empty<BalancePerson>(),
                expenses.GetValueOrDefault(tripId) ?? Array.Empty<BalanceExpense>(),
                payments.GetValueOrDefault(tripId) ?? Array.Empty<BalancePayment>()));
    }

    /// <summary>Row types as Npgsql returns them. Column aliases match the property names.</summary>
    internal sealed record PersonRow(Guid Id, string Name, long Seq);

    internal sealed record PaymentRow(Guid FromPersonId, Guid ToPersonId, long AmountCents);

    internal sealed record ExpenseRow(Guid Id, long AmountCents, Guid PayerId);

    internal sealed record ParticipantRow(Guid ExpenseId, Guid PersonId, long Seq);

    internal sealed record OwnerPersonRow(Guid TripId, Guid Id, string Name, long Seq);

    internal sealed record OwnerExpenseRow(Guid TripId, Guid Id, long AmountCents, Guid PayerId);

    internal sealed record OwnerPaymentRow(Guid TripId, Guid FromPersonId, Guid ToPersonId, long AmountCents);
}
