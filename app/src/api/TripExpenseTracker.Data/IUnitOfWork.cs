using Npgsql;

namespace TripExpenseTracker.Data;

/// <summary>
/// One connection and at most one transaction for a request or a test. DAOs in the same unit of work
/// share the connection, and while a transaction is active they run inside it. Scoped per request.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>The connection for this unit of work. It is opened on first use.</summary>
    Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken);

    /// <summary>The active transaction, or null when none is active. DAOs pass this to every command.</summary>
    NpgsqlTransaction? Transaction { get; }

    Task BeginAsync(CancellationToken cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}
