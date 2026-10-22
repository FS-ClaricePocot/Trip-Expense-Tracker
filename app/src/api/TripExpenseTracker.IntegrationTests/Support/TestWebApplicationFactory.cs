using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TripExpenseTracker.IntegrationTests.Support;

/// <summary>
/// Starts the API for tests without a database. Migrations are off and the connection string is
/// cleared, so user secrets from a developer's machine are never read and a test never touches a
/// shared or local database. Tests that need a database use <see cref="PostgresFixture"/> instead.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:MigrateOnStartup"] = "false",
                ["ConnectionStrings:Default"] = string.Empty,
            });
        });
    }
}
