using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using TripExpenseTracker.Core.Auth;

namespace TripExpenseTracker.Api.Auth;

/// <summary>
/// Authenticates a request from the tet_session cookie (F1.R6). The cookie holds only the session ID, not
/// the ASP.NET encrypted auth cookie. The session is checked on the server through AuthService on every
/// request, so logout and expiry take effect at once. Challenge and forbid return 401 ProblemDetails, never
/// a redirect.
/// </summary>
public sealed class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Session";

    public SessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var sessionId = Context.Request.Cookies[SessionCookie.Name];
        if (string.IsNullOrEmpty(sessionId))
        {
            return AuthenticateResult.NoResult();
        }

        // Resolved only when a cookie is present, so a request without one never reaches the database.
        var auth = Context.RequestServices.GetRequiredService<AuthService>();
        var user = await auth.ResolveSessionAsync(sessionId, Context.RequestAborted);
        if (user is null)
        {
            return AuthenticateResult.NoResult();
        }

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
            },
            SchemeName);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        WriteUnauthorizedAsync();

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        WriteUnauthorizedAsync();

    private Task WriteUnauthorizedAsync() =>
        Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.2")
        .ExecuteAsync(Context);
}
