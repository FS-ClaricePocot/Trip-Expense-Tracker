using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using TripExpenseTracker.IntegrationTests.Support;
using Xunit;

namespace TripExpenseTracker.IntegrationTests.Auth;

/// <summary>The body of register, login and me (Contract changes).</summary>
public sealed record UserBody(Guid Id, string Email);

/// <summary>
/// The four auth endpoints against a real PostgreSQL container (F1.R1 to R8, R10, R12). Each test uses its
/// own email, so tests in this class do not depend on each other. The clock is fixed and moved by hand.
/// </summary>
public class AuthEndpointTests : IClassFixture<PostgresFixture>, IDisposable
{
    private const string GoodPassword = "correct horse battery";
    private const string CookieName = "tet_session";

    private readonly PostgresFixture _db;
    private readonly DatabaseWebApplicationFactory _factory;

    public AuthEndpointTests(PostgresFixture db)
    {
        _db = db;
        _factory = new DatabaseWebApplicationFactory(db.ConnectionString);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Register_SignsIn_SetsCookie_AndMeReturnsTheUser()
    {
        using var client = NewClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", new { email = UniqueEmail(), password = GoodPassword });
        var sessionId = SessionIdFrom(register);
        var registered = await register.Content.ReadFromJsonAsync<UserBody>();
        var me = await client.SendAsync(WithSession(HttpMethod.Get, "/api/auth/me", sessionId));
        var current = await me.Content.ReadFromJsonAsync<UserBody>();

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(registered!.Id, current!.Id);
    }

    [Fact]
    public async Task Register_EmailDifferingOnlyByCase_ReturnsGeneric400_AndKeepsOneUser()
    {
        using var client = NewClient();
        var suffix = Guid.NewGuid().ToString("N");

        var first = await client.PostAsJsonAsync("/api/auth/register", new { email = $"Anna{suffix}@X.com", password = GoodPassword });
        var second = await client.PostAsJsonAsync("/api/auth/register", new { email = $"anna{suffix}@x.com", password = GoodPassword });
        var problem = await ReadProblemAsync(second);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("registration-failed", problem.GetProperty("type").GetString());
        Assert.False(problem.TryGetProperty("errors", out _));
        Assert.Equal(1, await CountUsersAsync($"anna{suffix}@x.com"));
    }

    [Theory]
    [InlineData(7, "password_too_short")]
    [InlineData(129, "password_too_long")]
    public async Task Register_PasswordOutOfRange_ReturnsFieldCode_AndCreatesNoUser(int length, string expectedCode)
    {
        using var client = NewClient();
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = new string('a', length) });
        var problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, problem.GetProperty("errors").GetProperty("password")[0].GetString());
        Assert.Equal(0, await CountUsersAsync(email));
    }

    [Fact]
    public async Task Register_TrimsAndLowersEmail_AndRejectsEmailWithoutAtSign()
    {
        using var client = NewClient();
        var suffix = Guid.NewGuid().ToString("N");
        var stored = $"anna{suffix}@x.com";

        var accepted = await client.PostAsJsonAsync("/api/auth/register", new { email = $" Anna{suffix}@x.com ", password = GoodPassword });
        var acceptedBody = await accepted.Content.ReadFromJsonAsync<UserBody>();
        var malformed = await client.PostAsJsonAsync("/api/auth/register", new { email = "anna.x.com", password = GoodPassword });
        var problem = await ReadProblemAsync(malformed);

        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Equal(stored, acceptedBody!.Email);
        Assert.Equal(1, await CountUsersAsync(stored));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("email_invalid", problem.GetProperty("errors").GetProperty("email")[0].GetString());
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_ReturnIdenticalResponses()
    {
        using var client = NewClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = GoodPassword });

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "not the password" });
        var unknownEmail = await client.PostAsJsonAsync("/api/auth/login", new { email = UniqueEmail(), password = "not the password" });
        var wrongBody = await wrongPassword.Content.ReadAsStringAsync();
        var unknownBody = await unknownEmail.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(wrongPassword.StatusCode, unknownEmail.StatusCode);
        Assert.Equal(wrongPassword.Content.Headers.ContentType?.MediaType, unknownEmail.Content.Headers.ContentType?.MediaType);
        Assert.Equal(wrongBody, unknownBody);
        Assert.Equal("invalid-credentials", ParseProblem(wrongBody).GetProperty("type").GetString());
    }

    [Fact]
    public async Task SessionCookie_HasRequiredFlags_NoExpiry_AndHoldsOnlyTheStoredSessionId()
    {
        using var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = UniqueEmail(), password = GoodPassword });
        var setCookie = SessionCookieHeader(response);
        var sessionId = SessionIdFrom(response);
        var user = await response.Content.ReadFromJsonAsync<UserBody>();

        Assert.NotNull(setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max-age", setCookie, StringComparison.OrdinalIgnoreCase);
        // Development is not Secure. The Production test covers the Secure flag.
        Assert.DoesNotContain("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        // The value is only the 32-byte base64url session ID, and it is the one row in sessions.
        Assert.Matches("^[A-Za-z0-9_-]{43}$", sessionId);
        Assert.Equal(1, await CountSessionsAsync(sessionId));
        Assert.Equal(user!.Id, await SessionUserIdAsync(sessionId));
    }

    [Fact]
    public async Task Logout_RemovesSessionRow_AndLaterMeReturns401()
    {
        using var client = NewClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new { email = UniqueEmail(), password = GoodPassword });
        var sessionId = SessionIdFrom(register);

        var logout = await client.SendAsync(WithSession(HttpMethod.Post, "/api/auth/logout", sessionId));
        var me = await client.SendAsync(WithSession(HttpMethod.Get, "/api/auth/me", sessionId));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(0, await CountSessionsAsync(sessionId));
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutSession_Returns204()
    {
        using var client = NewClient();

        var response = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task TwentyWrongLogins_DoNotBlockACorrectLogin()
    {
        using var client = NewClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = GoodPassword });

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "not the password" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        var correct = await client.PostAsJsonAsync("/api/auth/login", new { email, password = GoodPassword });

        Assert.Equal(HttpStatusCode.OK, correct.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutCookie_Returns401_WithProblemDetails_AndNoRedirect()
    {
        using var client = NewClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Session_At23h59m_StillWorks_AndAt24h01m_IsRejectedAndDeleted()
    {
        using var client = NewClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new { email = UniqueEmail(), password = GoodPassword });
        var sessionId = SessionIdFrom(register);

        _factory.Time.Advance(TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59));
        var before = await client.SendAsync(WithSession(HttpMethod.Get, "/api/auth/me", sessionId));

        _factory.Time.Advance(TimeSpan.FromMinutes(2));
        var after = await client.SendAsync(WithSession(HttpMethod.Get, "/api/auth/me", sessionId));

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Equal(0, await CountSessionsAsync(sessionId));
    }

    private HttpClient NewClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string UniqueEmail() => $"anna-{Guid.NewGuid():N}@x.com";

    private static string? SessionCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(value => value.StartsWith(CookieName + "=", StringComparison.Ordinal))
            : null;

    private static string SessionIdFrom(HttpResponseMessage response)
    {
        var header = SessionCookieHeader(response) ?? throw new InvalidOperationException("No tet_session cookie was set.");
        return header.Split(';')[0][(CookieName.Length + 1)..];
    }

    private static HttpRequestMessage WithSession(HttpMethod method, string path, string sessionId)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", $"{CookieName}={sessionId}");
        return request;
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response) =>
        ParseProblem(await response.Content.ReadAsStringAsync());

    private static JsonElement ParseProblem(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private async Task<object?> ScalarAsync(string sql, string value)
    {
        await using var connection = new NpgsqlConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("value", value);
        return await command.ExecuteScalarAsync();
    }

    private async Task<long> CountUsersAsync(string email) =>
        (long)(await ScalarAsync("SELECT count(*) FROM users WHERE email = @value", email))!;

    private async Task<long> CountSessionsAsync(string sessionId) =>
        (long)(await ScalarAsync("SELECT count(*) FROM sessions WHERE id = @value", sessionId))!;

    private async Task<Guid> SessionUserIdAsync(string sessionId) =>
        (Guid)(await ScalarAsync("SELECT user_id FROM sessions WHERE id = @value", sessionId))!;
}
