using Serilog;
using Testcontainers.PostgreSql;
using TripExpenseTracker.Data;
using Xunit;

namespace TripExpenseTracker.IntegrationTests.Support;

/// <summary>
/// Starts a fresh PostgreSQL container for each test class and runs the migrations once.
/// Because the container is new, every test class gets a clean database.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        MigrationRunner.Migrate(ConnectionString, Log.Logger);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
