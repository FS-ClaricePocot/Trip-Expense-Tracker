using Microsoft.AspNetCore.Identity;
using TripExpenseTracker.Core.Auth;

namespace TripExpenseTracker.Api.Auth;

/// <summary>Wraps ASP.NET <see cref="PasswordHasher{TUser}"/> (PBKDF2) behind the Core <see cref="IPasswordHasher"/> (F1.R3).</summary>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    // PasswordHasher<TUser> does not use the user object, so a fixed placeholder is passed.
    private static readonly object HashUser = new();

    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(HashUser, password);

    public bool Verify(string password, string passwordHash) =>
        _hasher.VerifyHashedPassword(HashUser, passwordHash, password) != PasswordVerificationResult.Failed;
}
