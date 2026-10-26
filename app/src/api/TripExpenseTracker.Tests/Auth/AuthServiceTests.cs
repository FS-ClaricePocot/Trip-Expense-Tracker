using FluentAssertions;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Tests.Support;

namespace TripExpenseTracker.Tests.Auth;

public class AuthServiceTests
{
    private const string Email = "anna@x.com";
    private const string Password = "password1";
    private static readonly DateTimeOffset Start = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeUserDao _users = new();
    private readonly FakeSessionDao _sessions = new();
    private readonly FakeHasher _hasher = new();
    private readonly FakeTransaction _transaction = new();
    private readonly FakeTimeProvider _time = new(Start);
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_users, _sessions, _hasher, _transaction, _time, new AuthOptions());
    }

    [Fact]
    public async Task Register_stores_a_hash_that_is_not_the_password_and_verifies()
    {
        var result = await _sut.RegisterAsync(Email, Password, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var stored = _users.Users.Should().ContainSingle().Subject;
        stored.PasswordHash.Should().NotBe(Password);
        _hasher.Verify(Password, stored.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Register_creates_the_user_and_the_session_in_one_transaction_and_logs_in()
    {
        var result = await _sut.RegisterAsync(" Anna@X.com ", Password, CancellationToken.None);

        _transaction.RunCount.Should().Be(1);
        result.User!.Email.Should().Be("anna@x.com");
        _sessions.Sessions.Should().ContainSingle()
            .Which.Id.Should().Be(result.User.SessionId);
        _sessions.Sessions[0].UserId.Should().Be(result.User.UserId);
    }

    [Fact]
    public async Task Register_with_a_taken_email_is_refused_with_no_second_account_or_session()
    {
        await _sut.RegisterAsync(Email, Password, CancellationToken.None);

        var result = await _sut.RegisterAsync("ANNA@x.com", Password, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().Be(AuthFailure.RegistrationRefused);
        _users.Users.Should().ContainSingle();
        _sessions.Sessions.Should().ContainSingle();
    }

    [Fact]
    public async Task Register_with_an_invalid_password_returns_field_errors_and_does_not_hash_or_write()
    {
        var result = await _sut.RegisterAsync(Email, "short", CancellationToken.None);

        result.Failure.Should().Be(AuthFailure.Validation);
        result.FieldErrors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new FieldError("password", "password_too_short"));
        _hasher.HashCount.Should().Be(0);
        _users.Users.Should().BeEmpty();
        _transaction.RunCount.Should().Be(0);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_return_the_same_failure()
    {
        await _sut.RegisterAsync(Email, Password, CancellationToken.None);

        var wrongPassword = await _sut.LoginAsync(Email, "wrongpass1", CancellationToken.None);
        var unknownEmail = await _sut.LoginAsync("nobody@x.com", Password, CancellationToken.None);

        wrongPassword.Succeeded.Should().BeFalse();
        unknownEmail.Succeeded.Should().BeFalse();
        wrongPassword.Should().BeEquivalentTo(unknownEmail);
        wrongPassword.Failure.Should().Be(AuthFailure.InvalidCredentials);
    }

    [Fact]
    public async Task Unknown_email_login_still_runs_one_verification_against_a_dummy_hash()
    {
        var result = await _sut.LoginAsync("nobody@x.com", Password, CancellationToken.None);

        result.Failure.Should().Be(AuthFailure.InvalidCredentials);
        var verification = _hasher.Verifications.Should().ContainSingle().Subject;
        verification.Password.Should().Be(Password);
        verification.Hash.Should().NotBeNullOrEmpty();
        _users.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task Login_with_a_password_over_128_characters_fails_without_hashing()
    {
        await _sut.RegisterAsync(Email, Password, CancellationToken.None);
        var hashesBefore = _hasher.HashCount;
        var verificationsBefore = _hasher.Verifications.Count;

        var result = await _sut.LoginAsync(Email, new string('a', 129), CancellationToken.None);

        result.Failure.Should().Be(AuthFailure.InvalidCredentials);
        _hasher.HashCount.Should().Be(hashesBefore);
        _hasher.Verifications.Count.Should().Be(verificationsBefore);
    }

    [Fact]
    public async Task Two_logins_give_different_session_ids()
    {
        await _sut.RegisterAsync(Email, Password, CancellationToken.None);

        var first = await _sut.LoginAsync(Email, Password, CancellationToken.None);
        var second = await _sut.LoginAsync(Email, Password, CancellationToken.None);

        first.User!.SessionId.Should().NotBe(second.User!.SessionId);
        // A session ID is 32 random bytes, base64url encoded: 43 characters, no padding.
        first.User.SessionId.Should().HaveLength(43);
        first.User.SessionId.Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Fact]
    public async Task Session_at_23h59m_resolves_and_at_24h01m_is_rejected_and_deleted()
    {
        var registered = await _sut.RegisterAsync(Email, Password, CancellationToken.None);
        var sessionId = registered.User!.SessionId;

        _time.Advance(TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59));
        var resolved = await _sut.ResolveSessionAsync(sessionId, CancellationToken.None);
        resolved.Should().NotBeNull();
        resolved!.Email.Should().Be(Email);

        _time.Advance(TimeSpan.FromMinutes(2));
        var expired = await _sut.ResolveSessionAsync(sessionId, CancellationToken.None);
        expired.Should().BeNull();
        _sessions.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Logout_deletes_the_session()
    {
        var registered = await _sut.RegisterAsync(Email, Password, CancellationToken.None);

        await _sut.LogoutAsync(registered.User!.SessionId, CancellationToken.None);

        _sessions.Sessions.Should().BeEmpty();
        (await _sut.ResolveSessionAsync(registered.User.SessionId, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Logout_with_no_session_or_an_unknown_session_does_nothing_and_does_not_fail()
    {
        await _sut.RegisterAsync(Email, Password, CancellationToken.None);

        var act = async () =>
        {
            await _sut.LogoutAsync(null, CancellationToken.None);
            await _sut.LogoutAsync("", CancellationToken.None);
            await _sut.LogoutAsync("no-such-session", CancellationToken.None);
        };

        await act.Should().NotThrowAsync();
        _sessions.Sessions.Should().ContainSingle();
    }

    private sealed class FakeUserDao : IUserDao
    {
        public List<User> Users { get; } = new();

        public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Email == normalizedEmail));

        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

        public Task<UserInsertResult> InsertAsync(User user, CancellationToken cancellationToken)
        {
            if (Users.Any(u => string.Equals(u.Email, user.Email, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(UserInsertResult.EmailTaken);
            }

            Users.Add(user);
            return Task.FromResult(UserInsertResult.Inserted);
        }
    }

    private sealed class FakeSessionDao : ISessionDao
    {
        public List<Session> Sessions { get; } = new();

        public Task InsertAsync(Session session, CancellationToken cancellationToken)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task<Session?> FindAsync(string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(Sessions.FirstOrDefault(s => s.Id == sessionId));

        public Task DeleteAsync(string sessionId, CancellationToken cancellationToken)
        {
            Sessions.RemoveAll(s => s.Id == sessionId);
            return Task.CompletedTask;
        }

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
        {
            var removed = Sessions.RemoveAll(s => s.CreatedAt < cutoff);
            return Task.FromResult(removed);
        }
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public int HashCount { get; private set; }

        public List<(string Password, string Hash)> Verifications { get; } = new();

        public string Hash(string password)
        {
            HashCount++;
            return "hash:" + password;
        }

        public bool Verify(string password, string passwordHash)
        {
            Verifications.Add((password, passwordHash));
            return passwordHash == "hash:" + password;
        }
    }

    private sealed class FakeTransaction : IAuthTransaction
    {
        public int RunCount { get; private set; }

        public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
        {
            RunCount++;
            return await work(cancellationToken);
        }
    }
}
