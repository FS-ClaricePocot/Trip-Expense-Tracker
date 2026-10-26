using Dapper;
using TripExpenseTracker.Core.Auth;

namespace TripExpenseTracker.Data;

public sealed class SessionDao : ISessionDao
{
    private const string InsertSql = """
        INSERT INTO sessions (id, user_id, created_at)
        VALUES (@Id, @UserId, @CreatedAt)
        """;

    private const string FindSql = """
        SELECT id AS "Id", user_id AS "UserId", created_at AS "CreatedAt"
        FROM sessions
        WHERE id = @Id
        """;

    private const string DeleteSql = "DELETE FROM sessions WHERE id = @Id";

    private const string DeleteOlderThanSql = "DELETE FROM sessions WHERE created_at < @Cutoff";

    private readonly IUnitOfWork _unitOfWork;

    public SessionDao(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task InsertAsync(Session session, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            InsertSql,
            new
            {
                Id = session.Id,
                UserId = session.UserId,
                CreatedAt = session.CreatedAt.ToUniversalTime(),
            },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<Session?> FindAsync(string sessionId, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            FindSql,
            new { Id = sessionId },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<SessionRow>(command);
        return row?.ToSession();
    }

    public async Task DeleteAsync(string sessionId, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            DeleteSql,
            new { Id = sessionId },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            DeleteOlderThanSql,
            new { Cutoff = cutoff.ToUniversalTime() },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command);
    }

    /// <summary>Reads a row with the types Npgsql returns. timestamptz comes back as UTC DateTime.</summary>
    internal sealed record SessionRow(string Id, Guid UserId, DateTime CreatedAt)
    {
        public Session ToSession() => new(Id, UserId, new DateTimeOffset(DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc)));
    }
}
