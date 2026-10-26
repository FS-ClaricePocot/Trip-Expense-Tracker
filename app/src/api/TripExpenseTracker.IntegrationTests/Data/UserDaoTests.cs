using System.Security.Cryptography;
using Npgsql;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Data;
using TripExpenseTracker.IntegrationTests.Support;
using Xunit;

namespace TripExpenseTracker.IntegrationTests.Data;

public class UserDaoTests : IClassFixture<PostgresFixture>
{
    // fixed instant, so the tests never depend on the current time
    private static readonly DateTimeOffset FixedCreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _fixture;
    private readonly NpgsqlConnectionFactory _factory;

    public UserDaoTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _factory = new NpgsqlConnectionFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task InsertThenFind_RoundTripsByEmailAndId()
    {
        var user = new User(Guid.NewGuid(), $"round-{Guid.NewGuid():N}@x.com", "hash-1", FixedCreatedAt);
        await using var uow = new UnitOfWork(_factory);
        var dao = new UserDao(uow);

        var result = await dao.InsertAsync(user, CancellationToken.None);
        var byEmail = await dao.FindByEmailAsync(user.Email, CancellationToken.None);
        var byId = await dao.FindByIdAsync(user.Id, CancellationToken.None);

        Assert.Equal(UserInsertResult.Inserted, result);
        Assert.Equal(user, byEmail);
        Assert.Equal(user, byId);
    }

    [Fact]
    public async Task InsertSameEmailDifferentCase_ReturnsEmailTakenAndKeepsFirstRow()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var first = new User(Guid.NewGuid(), $"Anna{suffix}@x.com", "hash-1", FixedCreatedAt);
        var second = new User(Guid.NewGuid(), $"anna{suffix}@x.com", "hash-2", FixedCreatedAt);
        await using var uow = new UnitOfWork(_factory);
        var dao = new UserDao(uow);

        var firstResult = await dao.InsertAsync(first, CancellationToken.None);
        var secondResult = await dao.InsertAsync(second, CancellationToken.None);
        var stored = await dao.FindByEmailAsync($"anna{suffix}@x.com", CancellationToken.None);

        Assert.Equal(UserInsertResult.Inserted, firstResult);
        Assert.Equal(UserInsertResult.EmailTaken, secondResult);
        Assert.Equal(first.Id, stored?.Id);
    }

    [Fact]
    public async Task TwentyParallelInserts_SameEmail_StoreExactlyOneRow()
    {
        var email = $"race-{Guid.NewGuid():N}@x.com";

        // Each insert has its own unit of work, so each runs on its own connection.
        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            await using var uow = new UnitOfWork(_factory);
            var user = new User(Guid.NewGuid(), email, "hash", FixedCreatedAt);
            return await new UserDao(uow).InsertAsync(user, CancellationToken.None);
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Single(results, r => r == UserInsertResult.Inserted);
        Assert.Equal(19, results.Count(r => r == UserInsertResult.EmailTaken));
        Assert.Equal(1, await CountUsersAsync(email));
    }

    [Fact]
    public async Task StoredPasswordHash_IsNeverThePlainPassword()
    {
        const string password = "correct horse battery";
        var hash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(
            password, RandomNumberGenerator.GetBytes(16), 10_000, HashAlgorithmName.SHA256, 32));
        var user = new User(Guid.NewGuid(), $"hash-{Guid.NewGuid():N}@x.com", hash, FixedCreatedAt);
        await using var uow = new UnitOfWork(_factory);
        var dao = new UserDao(uow);

        await dao.InsertAsync(user, CancellationToken.None);
        var stored = await dao.FindByIdAsync(user.Id, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.NotEqual(password, stored.PasswordHash);
        Assert.DoesNotContain(password, stored.PasswordHash);
    }

    private async Task<long> CountUsersAsync(string email)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE lower(email) = @email", conn);
        cmd.Parameters.AddWithValue("email", email);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}
