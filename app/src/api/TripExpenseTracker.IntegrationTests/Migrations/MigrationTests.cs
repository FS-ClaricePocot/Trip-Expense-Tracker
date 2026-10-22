using Npgsql;
using TripExpenseTracker.Data;
using TripExpenseTracker.IntegrationTests.Support;
using Serilog;
using Xunit;

namespace TripExpenseTracker.IntegrationTests.Migrations;

public class MigrationTests : IClassFixture<PostgresFixture>
{
    private const string FirstScriptPattern = "%0001_create_users_and_sessions.sql";

    // fixed instant, so the tests never depend on the current time
    private static readonly DateTimeOffset FixedCreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _fixture;

    public MigrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Migrate_CreatesUsersAndSessionsTablesAndCaseInsensitiveEmailIndex()
    {
        await using var conn = await OpenAsync();

        var tables = await ScalarAsync<long>(conn,
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('users', 'sessions')");
        var index = await ScalarAsync<long>(conn,
            "SELECT COUNT(*) FROM pg_indexes WHERE tablename = 'users' AND indexname = 'ux_users_email_lower' AND indexdef LIKE 'CREATE UNIQUE INDEX%lower(email)%'");

        Assert.Equal(2, tables);
        Assert.Equal(1, index);
    }

    [Fact]
    public async Task Users_HasPasswordHashColumnAndNoPlainTextPasswordColumn()
    {
        await using var conn = await OpenAsync();

        var columns = await ScalarAsync<long>(conn,
            "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'users' AND column_name = 'password_hash'");
        var plainText = await ScalarAsync<long>(conn,
            "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'users' AND column_name ILIKE '%password%' AND column_name <> 'password_hash'");

        Assert.Equal(1, columns);
        Assert.Equal(0, plainText);
    }

    [Fact]
    public async Task Insert_EmailDifferingOnlyInCase_FailsWithUniqueViolation()
    {
        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await InsertUserAsync(conn, tx, "Anna@x.com");

        var ex = await Assert.ThrowsAsync<PostgresException>(() => InsertUserAsync(conn, tx, "anna@x.com"));
        Assert.Equal("23505", ex.SqlState);

        // the transaction is rolled back on dispose, so no rows are left behind
    }

    [Fact]
    public async Task Migrate_RunTwice_ChangesNothing()
    {
        await using var conn = await OpenAsync();
        var before = await ScalarAsync<long>(conn,
            "SELECT COUNT(*) FROM schemaversions WHERE scriptname LIKE @pattern", ("pattern", FirstScriptPattern));

        MigrationRunner.Migrate(_fixture.ConnectionString, Log.Logger);

        var after = await ScalarAsync<long>(conn,
            "SELECT COUNT(*) FROM schemaversions WHERE scriptname LIKE @pattern", ("pattern", FirstScriptPattern));

        Assert.Equal(1, before);
        Assert.Equal(before, after);
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    private static async Task InsertUserAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string email)
    {
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO users (id, email, password_hash, created_at) VALUES (@id, @email, @hash, @createdAt)", conn, tx);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("hash", "test-hash");
        cmd.Parameters.AddWithValue("createdAt", FixedCreatedAt);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection conn, string sql, params (string Name, object Value)[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }
        var result = await cmd.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T));
    }
}
