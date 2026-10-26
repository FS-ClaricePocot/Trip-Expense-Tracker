using Microsoft.AspNetCore.Http;

namespace TripExpenseTracker.Api.Auth;

/// <summary>
/// The one browser cookie (F1.R6). It holds only the session ID. It is HttpOnly, SameSite=Lax, Path=/,
/// Secure outside Development, and has no Expires or Max-Age, so it lasts until logout or until the server
/// rejects the session.
/// </summary>
public static class SessionCookie
{
    public const string Name = "tet_session";

    public static void Append(HttpResponse response, string sessionId, bool secure) =>
        response.Cookies.Append(Name, sessionId, Options(secure));

    /// <summary>Clears the cookie. The browser removes it at once, so the expiry it sends is in the past.</summary>
    public static void Clear(HttpResponse response, bool secure) =>
        response.Cookies.Delete(Name, Options(secure));

    private static CookieOptions Options(bool secure) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = secure,
        Path = "/",
        IsEssential = true,
    };
}
