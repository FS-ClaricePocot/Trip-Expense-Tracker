using Dapper;
using TripExpenseTracker.Core.Auth;

namespace TripExpenseTracker.Data;

public sealed class UserDao : IUserDao
{
    // ON CONFLICT on the lower(email) index turns a duplicate into zero rows instead of an error.
    // An error would abort an active transaction in PostgreSQL, so the caller could not carry on in it.
    private const string InsertSql = """
        INSERT INTO users (id, email, password_hash, created_at)
        VALUES (@Id, @Email, @PasswordHash, @CreatedAt)
        ON CONFLICT (lower(email)) DO NOTHING
        """;

    private const string FindByEmailSql = """
        SELECT id AS "Id", email AS "Email", password_hash AS "PasswordHash", created_at AS "CreatedAt"
        FROM users
        WHERE lower(email) = @Email
        """;

    private const string FindByIdSql = """
        SELECT id AS "Id", email AS "Email", password_hash AS "PasswordHash", created_at AS "CreatedAt"
        FROM users
        WHERE id = @Id
        """;

    private readonly IUnitOfWork _unitOfWork;

    public UserDao(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            FindByEmailSql,
            new { Email = normalizedEmail },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(command);
        return row?.ToUser();
    }

    public async Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            FindByIdSql,
            new { Id = id },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(command);
        return row?.ToUser();
    }

    public async Task<UserInsertResult> InsertAsync(User user, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            InsertSql,
            new
            {
                Id = user.Id,
                Email = user.Email,
                PasswordHash = user.PasswordHash,
                CreatedAt = user.CreatedAt.ToUniversalTime(),
            },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.ExecuteAsync(command);
        return rows == 1 ? UserInsertResult.Inserted : UserInsertResult.EmailTaken;
    }

    /// <summary>
    /// Reads a row with the types Npgsql returns. timestamptz comes back as DateTime (UTC), not DateTimeOffset,
    /// so the row is converted to the domain type here.
    /// </summary>
    internal sealed record UserRow(Guid Id, string Email, string PasswordHash, DateTime CreatedAt)
    {
        public User ToUser() => new(Id, Email, PasswordHash, new DateTimeOffset(DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc)));
    }
}
