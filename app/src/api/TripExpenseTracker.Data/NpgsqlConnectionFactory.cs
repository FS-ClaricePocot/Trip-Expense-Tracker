using Npgsql;

namespace TripExpenseTracker.Data;

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string? _connectionString;

    /// <summary>
    /// The connection string is checked when a connection is opened, not here, so the API can start
    /// without a database (for example in tests that do not use one). The message never includes the string.
    /// </summary>
    public NpgsqlConnectionFactory(string? connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Default is not set.");
        }

        var connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
