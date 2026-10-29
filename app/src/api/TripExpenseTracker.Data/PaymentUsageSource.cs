using Dapper;
using TripExpenseTracker.Core.People;

namespace TripExpenseTracker.Data;

/// <summary>
/// Reports the people in recorded payments (F6.R16, F3.R7). A payer and a payee both count as "in a payment", so
/// removing either is refused. Joins trips and filters on owner_user_id, so another user's trip returns nobody.
/// Runs on the unit of work's connection, like the other DAOs.
/// </summary>
public sealed class PaymentUsageSource : IPersonUsageSource
{
    private const string PersonIdsSql = """
        SELECT pm.payer_person_id FROM payments pm
        JOIN trips t ON t.id = pm.trip_id
        WHERE t.id = @TripId AND t.owner_user_id = @Owner
        UNION
        SELECT pm.payee_person_id FROM payments pm
        JOIN trips t ON t.id = pm.trip_id
        WHERE t.id = @TripId AND t.owner_user_id = @Owner
        """;

    private readonly IUnitOfWork _unitOfWork;

    public PaymentUsageSource(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public PersonUsageKind Kind => PersonUsageKind.Payment;

    public async Task<IReadOnlyCollection<Guid>> GetPersonIdsInUseAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            PersonIdsSql,
            new { Owner = owner, TripId = tripId },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var ids = await connection.QueryAsync<Guid>(command);
        return ids.ToList();
    }
}
