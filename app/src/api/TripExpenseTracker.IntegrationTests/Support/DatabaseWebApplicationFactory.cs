using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TripExpenseTracker.IntegrationTests.Support;

/// <summary>
/// Starts the API against a Testcontainers database from <see cref="PostgresFixture"/>. Migrations are off
/// because the fixture runs them. The clock is a <see cref="FakeTimeProvider"/> that the test moves by hand,
/// so session age (F1.R6) never depends on the real time. The environment name controls the Secure cookie flag.
/// </summary>
public sealed class DatabaseWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _environmentName;

    public DatabaseWebApplicationFactory(string connectionString, string environmentName = "Development")
    {
        _connectionString = connectionString;
        _environmentName = environmentName;
    }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Records served by the test-only ownership probe (F1.R9). Only this factory registers the probe.</summary>
    public OwnershipProbeStore OwnershipProbe { get; } = new();

    /// <summary>Receives every event the API logs, after redaction (F1.AC10).</summary>
    public InMemoryLogSink Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:MigrateOnStartup"] = "false",
                ["ConnectionStrings:Default"] = _connectionString,
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);

            // The probe is test-only: its controller lives in this test assembly, so this factory adds that assembly as an MVC part.
            services.AddSingleton(OwnershipProbe);

            // The API reads extra Serilog sinks from DI (see Program.cs), so this sink sees the real pipeline's events.
            services.AddSingleton<Serilog.Core.ILogEventSink>(Logs);
            services.AddControllers().AddApplicationPart(typeof(OwnershipProbeController).Assembly);
        });
    }
}
