using Dapper;
using TripExpenseTracker.Core.People;

namespace TripExpenseTracker.Data;

/// <summary>
/// Reports the people in recorded payments (F6.R16, F3.R7). A payer and a payee both count as "in a payment", so
/// removing either is refused. Filtered on trip_id only: the service calls this only after the trip is confirmed to
/// belong to the owner. Runs on the unit of work's connection, like the other DAOs.
/// </summary>
public sealed class PaymentUsageSource : IPersonUsageSource
{
    private const string PersonIdsSql = """
        SELECT pm.payer_person_id FROM payments pm WHERE pm.trip_id = @TripId
        UNION
        SELECT pm.payee_person_id FROM payments pm WHERE pm.trip_id = @TripId
        """;

    private readonly IUnitOfWork _unitOfWork;

    public PaymentUsageSource(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public PersonUsageKind Kind => PersonUsageKind.Payment;

    public async Task<IReadOnlyCollection<Guid>> GetPersonIdsInUseAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            PersonIdsSql,
            new { TripId = tripId },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var ids = await connection.QueryAsync<Guid>(command);
        return ids.ToList();
    }
}
