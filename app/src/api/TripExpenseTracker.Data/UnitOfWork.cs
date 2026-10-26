using Npgsql;

namespace TripExpenseTracker.Data;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly IDbConnectionFactory _connectionFactory;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public UnitOfWork(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public NpgsqlTransaction? Transaction => _transaction;

    public async Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        _connection ??= await _connectionFactory.OpenAsync(cancellationToken);
        return _connection;
    }

    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already active.");
        }

        var connection = await GetConnectionAsync(cancellationToken);
        _transaction = await connection.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        var transaction = RequireTransaction();
        await transaction.CommitAsync(cancellationToken);
        await ClearTransactionAsync();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        var transaction = RequireTransaction();
        await transaction.RollbackAsync(cancellationToken);
        await ClearTransactionAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // Disposing a transaction that was neither committed nor rolled back rolls it back (Npgsql).
        await ClearTransactionAsync();

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    private NpgsqlTransaction RequireTransaction() =>
        _transaction ?? throw new InvalidOperationException("No transaction is active.");

    private async Task ClearTransactionAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
