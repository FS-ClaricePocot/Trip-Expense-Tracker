using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TripExpenseTracker.IntegrationTests.Support;
using Xunit;

namespace TripExpenseTracker.IntegrationTests.Auth;

/// <summary>
/// The Secure flag is set outside Development (F1.R6). The default factory runs in Development, so this
/// class starts the API as Production to check the flag.
/// </summary>
public class AuthCookieSecureTests : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly DatabaseWebApplicationFactory _factory;

    public AuthCookieSecureTests(PostgresFixture db)
    {
        _factory = new DatabaseWebApplicationFactory(db.ConnectionString, environmentName: "Production");
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SessionCookie_IsSecure_OutsideDevelopment_AndHasNoExpiry()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = $"secure-{Guid.NewGuid():N}@x.com", password = "correct horse battery" });
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("tet_session=", StringComparison.Ordinal));

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max-age", setCookie, StringComparison.OrdinalIgnoreCase);
    }
}
