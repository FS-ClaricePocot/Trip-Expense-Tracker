using Dapper;
using Npgsql;
using TripExpenseTracker.Core.Payments;

namespace TripExpenseTracker.Data;

/// <summary>
/// Dapper implementation of <see cref="IPaymentDao"/>. Every statement joins trips and filters on owner_user_id, so a
/// trip the user does not own looks the same as a missing one (F6.R15). Parameters only; no SQL is built from input.
/// Runs on the unit of work's transaction. The service takes <see cref="ITripLock"/> first.
/// </summary>
public sealed class PaymentDao : IPaymentDao
{
    // Payer and payee names come from the same trip (composite keys), so the joins always find both people.
    // Newest first by seq (F6.R7).
    private const string ListSql = """
        SELECT pm.id AS "Id", pm.trip_id AS "TripId", pm.payer_person_id AS "PayerPersonId",
               pm.payee_person_id AS "PayeePersonId", pm.amount_cents AS "AmountCents",
               pm.marked_paid_at AS "MarkedPaidAt", pm.seq AS "Seq",
               payer.name AS "PayerName", payee.name AS "PayeeName"
        FROM payments pm
        JOIN trips t ON t.id = pm.trip_id
        JOIN people payer ON payer.id = pm.payer_person_id AND payer.trip_id = pm.trip_id
        JOIN people payee ON payee.id = pm.payee_person_id AND payee.trip_id = pm.trip_id
        WHERE pm.trip_id = @TripId AND t.owner_user_id = @Owner
        ORDER BY pm.seq DESC
        """;

    // Zero rows means the trip is not the owner's or does not exist (F6.R15).
    private const string InsertSql = """
        INSERT INTO payments (id, trip_id, payer_person_id, payee_person_id, amount_cents, marked_paid_at)
        SELECT CAST(@Id AS uuid), t.id, CAST(@PayerPersonId AS uuid), CAST(@PayeePersonId AS uuid),
               CAST(@AmountCents AS bigint), CAST(@MarkedPaidAt AS timestamptz)
        FROM trips t
        WHERE t.id = @TripId AND t.owner_user_id = @Owner
        """;

    // The owner join in the USING clause means a payment on another user's trip matches no row (F6.R15).
    private const string DeleteSql = """
        DELETE FROM payments pm
        USING trips t
        WHERE pm.id = @PaymentId AND pm.trip_id = t.id AND t.id = @TripId AND t.owner_user_id = @Owner
        """;

    // A payer or payee that is not on this trip (removed after the service checked) is refused by the composite keys.
    private const string ForeignKeyViolation = "23503";
    private const string PayeeForeignKey = "fk_payments_payee";

    private readonly IUnitOfWork _unitOfWork;

    public PaymentDao(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<StoredPayment>> ListAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            ListSql,
            new { Owner = owner, TripId = tripId },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<PaymentRow>(command);
        return rows.Select(row => new StoredPayment(
            new Payment(
                row.Id,
                row.TripId,
                row.PayerPersonId,
                row.PayeePersonId,
                row.AmountCents,
                ToUtc(row.MarkedPaidAt)),
            row.Seq,
            row.PayerName,
            row.PayeeName)).ToList();
    }

    public async Task<PaymentInsertOutcome> InsertAsync(Guid owner, Payment payment, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.ExecuteAsync(new CommandDefinition(
                InsertSql,
                new
                {
                    Owner = owner,
                    TripId = payment.TripId,
                    Id = payment.Id,
                    PayerPersonId = payment.PayerPersonId,
                    PayeePersonId = payment.PayeePersonId,
                    AmountCents = payment.AmountCents,
                    MarkedPaidAt = payment.MarkedPaidAt.UtcDateTime,
                },
                transaction: _unitOfWork.Transaction,
                cancellationToken: cancellationToken));

            return rows > 0 ? PaymentInsertOutcome.Inserted : PaymentInsertOutcome.TripNotFound;
        }
        catch (PostgresException ex) when (ex.SqlState == ForeignKeyViolation)
        {
            // Payer or payee is not on the trip. The composite keys refuse the row (F6.R6), and the constraint
            // name says which one.
            return ex.ConstraintName == PayeeForeignKey
                ? PaymentInsertOutcome.PayeeNotOnTrip
                : PaymentInsertOutcome.PayerNotOnTrip;
        }
    }

    public async Task<bool> DeleteAsync(Guid owner, Guid tripId, Guid paymentId, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            DeleteSql,
            new { Owner = owner, TripId = tripId, PaymentId = paymentId },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.ExecuteAsync(command);
        return rows > 0;
    }

    private static DateTimeOffset ToUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    /// <summary>Reads a row with the types Npgsql returns. timestamptz comes back as DateTime, converted here.</summary>
    internal sealed record PaymentRow(
        Guid Id,
        Guid TripId,
        Guid PayerPersonId,
        Guid PayeePersonId,
        long AmountCents,
        DateTime MarkedPaidAt,
        long Seq,
        string PayerName,
        string PayeeName);
}
