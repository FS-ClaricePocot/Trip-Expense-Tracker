using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Data;
using TripExpenseTracker.IntegrationTests.Support;
using Xunit;

namespace TripExpenseTracker.IntegrationTests.Data;

public class SessionDaoTests : IClassFixture<PostgresFixture>
{
    // fixed instant, so the tests never depend on the current time
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly NpgsqlConnectionFactory _factory;

    public SessionDaoTests(PostgresFixture fixture)
    {
        _factory = new NpgsqlConnectionFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task InsertFindDelete_RoundTrip()
    {
        await using var uow = new UnitOfWork(_factory);
        var userId = await InsertUserAsync(uow);
        var dao = new SessionDao(uow);
        var session = new Session(NewSessionId(), userId, Now);

        await dao.InsertAsync(session, CancellationToken.None);
        var found = await dao.FindAsync(session.Id, CancellationToken.None);
        await dao.DeleteAsync(session.Id, CancellationToken.None);
        var afterDelete = await dao.FindAsync(session.Id, CancellationToken.None);

        Assert.Equal(session, found);
        Assert.Null(afterDelete);
    }

    [Fact]
    public async Task DeleteMissingSession_DoesNothing()
    {
        await using var uow = new UnitOfWork(_factory);
        var dao = new SessionDao(uow);

        await dao.DeleteAsync(NewSessionId(), CancellationToken.None);
    }

    [Fact]
    public async Task DeleteOlderThan_RemovesOnlySessionsCreatedBeforeCutoff()
    {
        await using var uow = new UnitOfWork(_factory);
        var userId = await InsertUserAsync(uow);
        var dao = new SessionDao(uow);
        var old = new Session(NewSessionId(), userId, Now.AddDays(-2));
        var recent = new Session(NewSessionId(), userId, Now.AddHours(-1));
        await dao.InsertAsync(old, CancellationToken.None);
        await dao.InsertAsync(recent, CancellationToken.None);

        // The table is shared by the tests in this class, so check the two rows by id, not the total count.
        var deleted = await dao.DeleteOlderThanAsync(Now.AddDays(-1), CancellationToken.None);

        Assert.True(deleted >= 1);
        Assert.Null(await dao.FindAsync(old.Id, CancellationToken.None));
        Assert.NotNull(await dao.FindAsync(recent.Id, CancellationToken.None));
    }

    private static async Task<Guid> InsertUserAsync(IUnitOfWork uow)
    {
        var id = Guid.NewGuid();
        var user = new User(id, $"session-{id:N}@x.com", "hash", Now);
        await new UserDao(uow).InsertAsync(user, CancellationToken.None);
        return id;
    }

    private static string NewSessionId() => Guid.NewGuid().ToString("N");
}
