using Dapper;
using TripExpenseTracker.Core.People;

namespace TripExpenseTracker.Data;

/// <summary>
/// Reports the people an expense refers to (F3.R7). A payer counts as "in an expense" even when they are not a
/// participant, because the payer's reference blocks removal. Joins trips and filters on owner_user_id, so another
/// user's trip returns nobody. Runs on the unit of work's connection, like the other DAOs.
/// </summary>
public sealed class ExpenseUsageSource : IPersonUsageSource
{
    private const string PersonIdsSql = """
        SELECT e.payer_person_id FROM expenses e
        JOIN trips t ON t.id = e.trip_id
        WHERE t.id = @TripId AND t.owner_user_id = @Owner
        UNION
        SELECT ep.person_id FROM expense_participants ep
        JOIN trips t ON t.id = ep.trip_id
        WHERE t.id = @TripId AND t.owner_user_id = @Owner
        """;

    private readonly IUnitOfWork _unitOfWork;

    public ExpenseUsageSource(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public PersonUsageKind Kind => PersonUsageKind.Expense;

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
