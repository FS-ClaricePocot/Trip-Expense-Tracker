namespace TripExpenseTracker.Core.Auth;

/// <summary>Outcome of inserting a user. A taken email is a normal result, not an exception (F1.R2).</summary>
public enum UserInsertResult
{
    Inserted,
    EmailTaken,
}

/// <summary>Data access for users. Implemented with Dapper in TripExpenseTracker.Data.</summary>
public interface IUserDao
{
    /// <summary>Looks up by an already normalized email (trimmed, lower-cased).</summary>
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the user. Returns <see cref="UserInsertResult.EmailTaken"/> when another account already uses
    /// the email, compared case-insensitively. The database unique index is the final check (F1.R2).
    /// A duplicate email never throws to the caller.
    /// </summary>
    Task<UserInsertResult> InsertAsync(User user, CancellationToken cancellationToken);
}
