using Npgsql;

namespace TripExpenseTracker.Data;

/// <summary>Opens a new open connection to the application database.</summary>
public interface IDbConnectionFactory
{
    Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken);
}
