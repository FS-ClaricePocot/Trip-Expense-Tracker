using System.Net.Http;

namespace TripExpenseTracker.IntegrationTests.Support;

/// <summary>
/// Session helpers shared by the auth integration tests. The cookie is sent by hand because the
/// WebApplicationFactory client keeps no cookie container, so each test chooses which user it acts as.
/// </summary>
public static class AuthTestHelpers
{
    private const string CookieName = "tet_session";

    public static string UniqueEmail() => $"anna-{Guid.NewGuid():N}@x.com";

    public static string? SessionCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(value => value.StartsWith(CookieName + "=", StringComparison.Ordinal))
            : null;

    public static string SessionIdFrom(HttpResponseMessage response)
    {
        var header = SessionCookieHeader(response) ?? throw new InvalidOperationException("No tet_session cookie was set.");
        return header.Split(';')[0][(CookieName.Length + 1)..];
    }

    public static HttpRequestMessage WithSession(HttpMethod method, string path, string sessionId)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", $"{CookieName}={sessionId}");
        return request;
    }
}
