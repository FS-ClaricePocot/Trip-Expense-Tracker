using System.Runtime.CompilerServices;

namespace TripExpenseTracker.Core.Auth;

public enum AuthFailure
{
    /// <summary>Field checks failed. See <see cref="AuthResult.FieldErrors"/>.</summary>
    Validation,

    /// <summary>Taken email or other refusal. Generic on purpose (F1.R8).</summary>
    RegistrationRefused,

    /// <summary>Unknown email or wrong password. The same result for both (F1.R8).</summary>
    InvalidCredentials,
}

public sealed record SignedInUser(Guid UserId, string Email, string SessionId);

/// <summary>Either a signed-in user, or a failure. Never both.</summary>
public sealed record AuthResult(SignedInUser? User, AuthFailure? Failure, IReadOnlyList<FieldError> FieldErrors)
{
    public bool Succeeded => User is not null;

    public static AuthResult SignedIn(SignedInUser user) => new(user, null, Array.Empty<FieldError>());

    public static AuthResult Fail(AuthFailure failure, IReadOnlyList<FieldError>? fieldErrors = null) =>
        new(null, failure, fieldErrors ?? Array.Empty<FieldError>());
}

/// <summary>
/// Registration, login, logout and session checks (F1.R1, R4 to R8). Pure rules over DAO interfaces and
/// injected time. No I/O of its own.
/// </summary>
public sealed class AuthService
{
    // One dummy hash per hasher, so unknown-email logins cost the same as wrong-password logins (F1.R8).
    // Shared across scoped instances, so the dummy is not re-hashed on every request.
    private static readonly ConditionalWeakTable<IPasswordHasher, string> DummyHashes = new();

    private readonly IUserDao _users;
    private readonly ISessionDao _sessions;
    private readonly IPasswordHasher _hasher;
    private readonly IAuthTransaction _transaction;
    private readonly TimeProvider _time;
    private readonly AuthOptions _options;

    public AuthService(
        IUserDao users,
        ISessionDao sessions,
        IPasswordHasher hasher,
        IAuthTransaction transaction,
        TimeProvider time,
        AuthOptions options)
    {
        _users = users;
        _sessions = sessions;
        _hasher = hasher;
        _transaction = transaction;
        _time = time;
        _options = options;
    }

    /// <summary>Validates, hashes, then creates the user and the session in one transaction (F1.R7).</summary>
    public async Task<AuthResult> RegisterAsync(string? email, string? password, CancellationToken cancellationToken)
    {
        var validation = RegistrationValidator.Validate(email, password);
        if (!validation.IsValid)
        {
            return AuthResult.Fail(AuthFailure.Validation, validation.Errors);
        }

        var now = _time.GetUtcNow();
        var user = new User(Guid.NewGuid(), validation.NormalizedEmail, _hasher.Hash(password!), now);
        var sessionId = SessionIdGenerator.Create();
        var session = new Session(sessionId, user.Id, now);

        var created = await _transaction.RunAsync(async token =>
        {
            if (await _users.InsertAsync(user, token) == UserInsertResult.EmailTaken)
            {
                return false;
            }

            await _sessions.InsertAsync(session, token);
            return true;
        }, cancellationToken);

        return created
            ? AuthResult.SignedIn(new SignedInUser(user.Id, user.Email, sessionId))
            : AuthResult.Fail(AuthFailure.RegistrationRefused);
    }

    /// <summary>
    /// Checks the password and starts a session. An unknown email still runs one verification against a
    /// dummy hash, so timing does not reveal whether the email exists (F1.R8). A password longer than the
    /// maximum fails before any hashing.
    /// </summary>
    public async Task<AuthResult> LoginAsync(string? email, string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(password) || PasswordPolicy.CountCharacters(password) > PasswordPolicy.MaxLength)
        {
            return AuthResult.Fail(AuthFailure.InvalidCredentials);
        }

        var user = await _users.FindByEmailAsync(EmailAddress.Normalize(email), cancellationToken);
        if (user is null)
        {
            _ = _hasher.Verify(password, DummyHash());
            return AuthResult.Fail(AuthFailure.InvalidCredentials);
        }

        if (!_hasher.Verify(password, user.PasswordHash))
        {
            return AuthResult.Fail(AuthFailure.InvalidCredentials);
        }

        var sessionId = SessionIdGenerator.Create();
        await _sessions.InsertAsync(new Session(sessionId, user.Id, _time.GetUtcNow()), cancellationToken);
        return AuthResult.SignedIn(new SignedInUser(user.Id, user.Email, sessionId));
    }

    /// <summary>Deletes the server-side session. Does nothing when there is no session or it is already gone (F1.R5).</summary>
    public Task LogoutAsync(string? sessionId, CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(sessionId)
            ? Task.CompletedTask
            : _sessions.DeleteAsync(sessionId, cancellationToken);

    /// <summary>
    /// Returns the user for a valid session. A session older than <see cref="AuthOptions.SessionMaxAge"/> is
    /// deleted and rejected (F1.R6). Returns null when there is no valid session.
    /// </summary>
    public async Task<User?> ResolveSessionAsync(string? sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return null;
        }

        var session = await _sessions.FindAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        if (_time.GetUtcNow() - session.CreatedAt > _options.SessionMaxAge)
        {
            await _sessions.DeleteAsync(sessionId, cancellationToken);
            return null;
        }

        return await _users.FindByIdAsync(session.UserId, cancellationToken);
    }

    private string DummyHash() =>
        DummyHashes.GetValue(_hasher, hasher => hasher.Hash(SessionIdGenerator.Create()));
}
