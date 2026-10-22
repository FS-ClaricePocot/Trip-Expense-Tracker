using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TripExpenseTracker.Api.Auth;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Data;
using TripExpenseTracker.IntegrationTests.Support;
using Xunit;
using static TripExpenseTracker.IntegrationTests.Support.AuthTestHelpers;

namespace TripExpenseTracker.IntegrationTests.Auth;

/// <summary>
/// One cleanup run against a real database (F1.R6). The factory does not start the background loop, so the test
/// runs a single pass by hand. The clock is a <see cref="FakeTimeProvider"/> that the test moves.
/// </summary>
public class SessionCleanupTests : IClassFixture<PostgresFixture>, IDisposable
{
    private const string GoodPassword = "correct horse battery";

    private readonly PostgresFixture _db;
    private readonly DatabaseWebApplicationFactory _factory;

    public SessionCleanupTests(PostgresFixture db)
    {
        _db = db;
        _factory = new DatabaseWebApplicationFactory(db.ConnectionString);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task OneRun_DeletesA25HourOldSession_AndKeepsA1HourOldOne()
    {
        using var client = _factory.CreateClient();

        // Registering creates a session at the current fake time.
        var oldRegister = await client.PostAsJsonAsync("/api/auth/register", new { email = UniqueEmail(), password = GoodPassword });
        var oldSessionId = SessionIdFrom(oldRegister);

        _factory.Time.Advance(TimeSpan.FromHours(24));
        var recentRegister = await client.PostAsJsonAsync("/api/auth/register", new { email = UniqueEmail(), password = GoodPassword });
        var recentSessionId = SessionIdFrom(recentRegister);

        // Now the first session is 25 hours old and the second is 1 hour old.
        _factory.Time.Advance(TimeSpan.FromHours(1));

        var cleanup = _factory.Services.GetRequiredService<SessionCleanupService>();
        await cleanup.CleanupOnceAsync(CancellationToken.None);

        await using var uow = new UnitOfWork(new NpgsqlConnectionFactory(_db.ConnectionString));
        var sessions = new SessionDao(uow);
        Assert.Null(await sessions.FindAsync(oldSessionId, CancellationToken.None));
        Assert.NotNull(await sessions.FindAsync(recentSessionId, CancellationToken.None));
    }
}
