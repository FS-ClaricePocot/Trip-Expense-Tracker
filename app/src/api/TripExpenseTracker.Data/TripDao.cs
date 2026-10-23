using Dapper;
using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Data;

/// <summary>
/// Dapper implementation of <see cref="ITripDao"/>. Every statement filters on owner_user_id, so another user's
/// trip looks the same as a missing one (F2.R9). Parameters only; no SQL is built from input.
/// </summary>
public sealed class TripDao : ITripDao
{
    private const string ListSql = """
        SELECT id AS "Id", owner_user_id AS "OwnerUserId", name AS "Name", created_at AS "CreatedAt"
        FROM trips
        WHERE owner_user_id = @Owner
        ORDER BY created_at DESC, id DESC
        """;

    // The people count sits in the same owner-filtered statement, so a trip the owner does not own returns no row (F2.R9, F3.R11).
    private const string GetSql = """
        SELECT t.id AS "Id", t.owner_user_id AS "OwnerUserId", t.name AS "Name", t.created_at AS "CreatedAt",
               (SELECT COUNT(*)::int FROM people p WHERE p.trip_id = t.id) AS "PeopleCount"
        FROM trips t
        WHERE t.owner_user_id = @Owner AND t.id = @Id
        """;

    private const string CountSql = """
        SELECT COUNT(*)::int
        FROM trips
        WHERE owner_user_id = @Owner
        """;

    // lower() on both sides, so "Paris" and "paris" are the same name for this owner only (F2.R3).
    private const string ExistsWithNameSql = """
        SELECT EXISTS (
            SELECT 1
            FROM trips
            WHERE owner_user_id = @Owner AND lower(name) = lower(@Name)
        )
        """;

    private const string InsertSql = """
        INSERT INTO trips (id, owner_user_id, name, created_at)
        VALUES (@Id, @OwnerUserId, @Name, @CreatedAt)
        """;

    private const string DeleteSql = """
        DELETE FROM trips
        WHERE id = @Id AND owner_user_id = @Owner
        """;

    // Row lock on the owner's user row. It is taken before the count and insert, so parallel creates for one owner run one at a time (F2.R4).
    private const string LockOwnerSql = """
        SELECT id FROM users WHERE id = @Owner FOR UPDATE
        """;

    private readonly IUnitOfWork _unitOfWork;

    public TripDao(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<Trip>> ListAsync(Guid owner, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            ListSql,
            new { Owner = owner },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<TripRow>(command);
        return rows.Select(row => row.ToTrip()).ToList();
    }

    public async Task<Trip?> GetAsync(Guid owner, Guid id, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            GetSql,
            new { Owner = owner, Id = id },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<TripDetailRow>(command);
        return row?.ToTrip();
    }

    public async Task<int> CountAsync(Guid owner, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            CountSql,
            new { Owner = owner },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<bool> ExistsWithNameAsync(Guid owner, string name, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            ExistsWithNameSql,
            new { Owner = owner, Name = name },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(command);
    }

    public async Task InsertAsync(Trip trip, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            InsertSql,
            new
            {
                Id = trip.Id,
                OwnerUserId = trip.OwnerUserId,
                Name = trip.Name,
                CreatedAt = trip.CreatedAt.UtcDateTime,
            },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<bool> DeleteAsync(Guid owner, Guid id, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            DeleteSql,
            new { Owner = owner, Id = id },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);
        var rows = await connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task LockOwnerAsync(Guid owner, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            LockOwnerSql,
            new { Owner = owner },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteScalarAsync<Guid?>(command);
    }

    /// <summary>
    /// Reads a row with the types Npgsql returns. timestamptz comes back as DateTime (UTC), not DateTimeOffset,
    /// so the row is converted to the domain type here (same as UserDao).
    /// </summary>
    internal sealed record TripRow(Guid Id, Guid OwnerUserId, string Name, DateTime CreatedAt)
    {
        public Trip ToTrip() => new(Id, OwnerUserId, Name, new DateTimeOffset(DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc)));
    }

    /// <summary>The detail read: a trip row plus its people count (F3.R11).</summary>
    internal sealed record TripDetailRow(Guid Id, Guid OwnerUserId, string Name, DateTime CreatedAt, int PeopleCount)
    {
        public Trip ToTrip() => new(Id, OwnerUserId, Name, new DateTimeOffset(DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc)), PeopleCount);
    }
}
