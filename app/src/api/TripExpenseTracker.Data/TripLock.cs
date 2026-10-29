using Dapper;
using TripExpenseTracker.Core.Payments;

namespace TripExpenseTracker.Data;

/// <summary>
/// Dapper implementation of <see cref="ITripLock"/> (F6.R18). Takes a row lock on the owner's trip with
/// <c>SELECT ... FOR UPDATE</c> on the unit of work's transaction, so the lock lasts until that transaction ends.
/// The owner is in the WHERE clause, so a foreign trip and a missing trip both return no row (F6.R15).
/// Must run inside an active transaction; without one the lock is released as soon as the statement ends.
/// </summary>
public sealed class TripLock : ITripLock
{
    private const string LockSql = """
        SELECT t.id FROM trips t WHERE t.id = @TripId AND t.owner_user_id = @Owner FOR UPDATE
        """;

    private readonly IUnitOfWork _unitOfWork;

    public TripLock(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> LockAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            LockSql,
            new { Owner = owner, TripId = tripId },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var locked = await connection.QueryFirstOrDefaultAsync<Guid?>(command);
        return locked is not null;
    }
}
