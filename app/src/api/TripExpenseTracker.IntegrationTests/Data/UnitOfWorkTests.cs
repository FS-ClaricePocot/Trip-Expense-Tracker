using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Data;
using TripExpenseTracker.IntegrationTests.Support;
using Xunit;

namespace TripExpenseTracker.IntegrationTests.Data;

public class UnitOfWorkTests : IClassFixture<PostgresFixture>
{
    // fixed instant, so the tests never depend on the current time
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly NpgsqlConnectionFactory _factory;

    public UnitOfWorkTests(PostgresFixture fixture)
    {
        _factory = new NpgsqlConnectionFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task RolledBackUnitOfWork_LeavesNoUserAndNoSession()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString("N");
        var email = $"rollback-{userId:N}@x.com";

        await using (var uow = new UnitOfWork(_factory))
        {
            await uow.BeginAsync(CancellationToken.None);
            await new UserDao(uow).InsertAsync(new User(userId, email, "hash", Now), CancellationToken.None);
            await new SessionDao(uow).InsertAsync(new Session(sessionId, userId, Now), CancellationToken.None);
            await uow.RollbackAsync(CancellationToken.None);
        }

        await using var check = new UnitOfWork(_factory);
        Assert.Null(await new UserDao(check).FindByIdAsync(userId, CancellationToken.None));
        Assert.Null(await new SessionDao(check).FindAsync(sessionId, CancellationToken.None));
    }

    [Fact]
    public async Task CommittedUnitOfWork_PersistsUserAndSession()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString("N");
        var email = $"commit-{userId:N}@x.com";

        await using (var uow = new UnitOfWork(_factory))
        {
            await uow.BeginAsync(CancellationToken.None);
            await new UserDao(uow).InsertAsync(new User(userId, email, "hash", Now), CancellationToken.None);
            await new SessionDao(uow).InsertAsync(new Session(sessionId, userId, Now), CancellationToken.None);
            await uow.CommitAsync(CancellationToken.None);
        }

        await using var check = new UnitOfWork(_factory);
        Assert.NotNull(await new UserDao(check).FindByIdAsync(userId, CancellationToken.None));
        Assert.NotNull(await new SessionDao(check).FindAsync(sessionId, CancellationToken.None));
    }
}
