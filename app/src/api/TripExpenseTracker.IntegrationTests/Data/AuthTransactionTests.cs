using Npgsql;
using Serilog;
using TripExpenseTracker.Api.Auth;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Data;
using TripExpenseTracker.IntegrationTests.Support;
using Xunit;

namespace TripExpenseTracker.IntegrationTests.Data;

public class AuthTransactionTests : IClassFixture<PostgresFixture>
{
    // fixed instant, so the tests never depend on the current time
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private const string Password = "correct horse battery staple";

    private readonly NpgsqlConnectionFactory _factory;

    public AuthTransactionTests(PostgresFixture fixture)
    {
        _factory = new NpgsqlConnectionFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task RegisterWhereSessionInsertFails_LeavesNoUserAndNoSession()
    {
        var email = $"tx-fail-{Guid.NewGuid():N}@x.com";

        await using var uow = new UnitOfWork(_factory);
        var users = new UserDao(uow);
        var sessions = new FailingSessionDao(new SessionDao(uow), users, email);
        var service = BuildService(uow, users, sessions);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegisterAsync(email, Password, CancellationToken.None));

        // the user insert succeeded inside the transaction before the session insert failed
        Assert.True(sessions.UserVisibleInTransaction);
        Assert.Single(sessions.AttemptedSessionIds);

        await using var check = new UnitOfWork(_factory);
        Assert.Null(await new UserDao(check).FindByEmailAsync(email, CancellationToken.None));
        Assert.Null(await new SessionDao(check).FindAsync(sessions.AttemptedSessionIds[0], CancellationToken.None));
    }

    [Fact]
    public async Task RegisterThatSucceeds_PersistsUserAndSession()
    {
        var email = $"tx-ok-{Guid.NewGuid():N}@x.com";

        string sessionId;
        await using (var uow = new UnitOfWork(_factory))
        {
            var users = new UserDao(uow);
            var service = BuildService(uow, users, new SessionDao(uow));

            var result = await service.RegisterAsync(email, Password, CancellationToken.None);

            Assert.True(result.Succeeded);
            sessionId = result.User!.SessionId;
        }

        await using var check = new UnitOfWork(_factory);
        Assert.NotNull(await new UserDao(check).FindByEmailAsync(email, CancellationToken.None));
        Assert.NotNull(await new SessionDao(check).FindAsync(sessionId, CancellationToken.None));
    }

    [Fact]
    public async Task WorkFailsAndRollbackFails_RethrowsOriginalError()
    {
        var uow = new ThrowingRollbackUnitOfWork();
        var tx = new AuthTransaction(uow, new LoggerConfiguration().CreateLogger());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tx.RunAsync<int>(_ => throw new InvalidOperationException("original failure"), CancellationToken.None));

        Assert.Equal("original failure", ex.Message);
        Assert.Equal(1, uow.RollbackAttempts);
        Assert.Equal(0, uow.CommitAttempts);
    }

    /// <summary>The real AuthService, with the scoped DAOs, the real adapter and a fixed clock.</summary>
    private static AuthService BuildService(IUnitOfWork uow, IUserDao users, ISessionDao sessions) =>
        new(
            users,
            sessions,
            new PasswordHasherAdapter(),
            new AuthTransaction(uow, new LoggerConfiguration().CreateLogger()),
            new FixedTimeProvider(Now),
            new AuthOptions());

    /// <summary>Unit of work whose rollback always throws. Begin succeeds and commit is never expected.</summary>
    private sealed class ThrowingRollbackUnitOfWork : IUnitOfWork
    {
        public int RollbackAttempts { get; private set; }

        public int CommitAttempts { get; private set; }

        public NpgsqlTransaction? Transaction => null;

        public Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("no database in this test");

        public Task BeginAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            CommitAttempts++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            RollbackAttempts++;
            throw new TimeoutException("rollback failed");
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Session DAO that records the attempted insert, then throws. Reads are delegated to the real DAO.</summary>
    private sealed class FailingSessionDao : ISessionDao
    {
        private readonly ISessionDao _inner;
        private readonly IUserDao _users;
        private readonly string _email;

        public FailingSessionDao(ISessionDao inner, IUserDao users, string email)
        {
            _inner = inner;
            _users = users;
            _email = email;
        }

        public List<string> AttemptedSessionIds { get; } = new();

        public bool UserVisibleInTransaction { get; private set; }

        public async Task InsertAsync(Session session, CancellationToken cancellationToken)
        {
            UserVisibleInTransaction = await _users.FindByEmailAsync(_email, cancellationToken) is not null;
            AttemptedSessionIds.Add(session.Id);
            throw new InvalidOperationException("session insert failed");
        }

        public Task<Session?> FindAsync(string sessionId, CancellationToken cancellationToken) =>
            _inner.FindAsync(sessionId, cancellationToken);

        public Task DeleteAsync(string sessionId, CancellationToken cancellationToken) =>
            _inner.DeleteAsync(sessionId, cancellationToken);

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
            _inner.DeleteOlderThanAsync(cutoff, cancellationToken);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
