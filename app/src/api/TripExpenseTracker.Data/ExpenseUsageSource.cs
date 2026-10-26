using Dapper;
using TripExpenseTracker.Core.People;

namespace TripExpenseTracker.Data;

/// <summary>
/// Reports the people an expense refers to (F3.R7). A payer counts as "in an expense" even when they are not a
/// participant, because the payer's reference blocks removal. Filtered on trip_id only: the service calls this only
/// after the trip is confirmed to belong to the owner. Runs on the unit of work's connection, like the other DAOs.
/// </summary>
public sealed class ExpenseUsageSource : IPersonUsageSource
{
    private const string PersonIdsSql = """
        SELECT e.payer_person_id FROM expenses e WHERE e.trip_id = @TripId
        UNION
        SELECT ep.person_id FROM expense_participants ep WHERE ep.trip_id = @TripId
        """;

    private readonly IUnitOfWork _unitOfWork;

    public ExpenseUsageSource(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public PersonUsageKind Kind => PersonUsageKind.Expense;

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
