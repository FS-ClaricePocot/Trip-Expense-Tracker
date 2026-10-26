using System.Net;
using System.Net.Http.Json;
using Npgsql;
using TripExpenseTracker.IntegrationTests.Support;
using static TripExpenseTracker.IntegrationTests.Support.AuthTestHelpers;

namespace TripExpenseTracker.IntegrationTests.Logging;

/// <summary>
/// Serial collection. Serilog's global Log.Logger is replaced each time a host starts, so another host running at
/// the same time would take this test's events. Running alone keeps the capture on this test's host.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LogCaptureCollection
{
    public const string Name = "LogCapture";
}

/// <summary>
/// Runs register, login, a failed login and logout through the real API pipeline, then checks every event the
/// pipeline logged. No password, stored hash or session ID may appear (F1.AC10, F1.R11). The clock is fixed.
/// </summary>
[Collection(LogCaptureCollection.Name)]
public class LogCaptureTests : IClassFixture<PostgresFixture>, IDisposable
{
    private const string Password = "correct horse battery";
    private const string WrongPassword = "wrong guess 98765";

    private readonly PostgresFixture _db;
    private readonly DatabaseWebApplicationFactory _factory;

    public LogCaptureTests(PostgresFixture db)
    {
        _db = db;
        _factory = new DatabaseWebApplicationFactory(db.ConnectionString);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task RegisterLoginFailedLoginAndLogout_LogNoPasswordHashOrSessionId()
    {
        using var client = _factory.CreateClient();
        var email = UniqueEmail();

        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        var registerSessionId = SessionIdFrom(register);
        var storedHash = await StoredHashAsync(email);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        var loginSessionId = SessionIdFrom(login);

        var failedLogin = await client.PostAsJsonAsync("/api/auth/login", new { email, password = WrongPassword });

        var logout = await client.SendAsync(WithSession(HttpMethod.Post, "/api/auth/logout", loginSessionId));

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, failedLogin.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.NotNull(storedHash);

        var logged = _factory.Logs.Snapshot();

        // The capture must see the real pipeline: request events for each call, and the failed-login event.
        Assert.Contains(logged, e => e.RenderMessage().Contains("/api/auth/login"));
        Assert.Contains(logged, e => e.RenderMessage() == "Login failed");

        var secrets = new[] { Password, WrongPassword, storedHash!, registerSessionId, loginSessionId };
        foreach (var logEvent in logged)
        {
            var text = string.Join(
                "\n",
                logEvent.RenderMessage(),
                logEvent.Exception?.ToString() ?? string.Empty,
                string.Join("\n", logEvent.Properties.Values.Select(value => value.ToString())));

            foreach (var secret in secrets)
            {
                Assert.DoesNotContain(secret, text);
            }
        }
    }

    private async Task<string?> StoredHashAsync(string email)
    {
        await using var connection = new NpgsqlConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT password_hash FROM users WHERE email = @value", connection);
        command.Parameters.AddWithValue("value", email);
        return (string?)await command.ExecuteScalarAsync();
    }
}
