namespace TripExpenseTracker.Core.Auth;

/// <summary>Data access for users. Implemented with Dapper in TripExpenseTracker.Data.</summary>
public interface IUserDao
{
    /// <summary>Looks up by an already normalized email (trimmed, lower-cased).</summary>
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Throws on a duplicate email; the database unique index is the final check (F1.R2).</summary>
    Task InsertAsync(User user, CancellationToken cancellationToken);
}
