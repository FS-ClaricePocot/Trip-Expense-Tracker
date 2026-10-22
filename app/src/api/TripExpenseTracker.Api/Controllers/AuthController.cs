using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripExpenseTracker.Api.Auth;
using TripExpenseTracker.Core.Auth;

namespace TripExpenseTracker.Api.Controllers;

/// <summary>
/// Register, login, logout and me (Contract changes). Everything else is protected by the fallback policy,
/// so only the four endpoints below opt out with AllowAnonymous.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private const string RegistrationFailedType = "registration-failed";
    private const string InvalidCredentialsType = "invalid-credentials";

    private readonly AuthService _auth;
    private readonly IWebHostEnvironment _environment;

    public AuthController(AuthService auth, IWebHostEnvironment environment)
    {
        _auth = auth;
        _environment = environment;
    }

    // The cookie is Secure everywhere except Development (F1.R6).
    private bool SecureCookie => !_environment.IsDevelopment();

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] CredentialsRequest request, CancellationToken cancellationToken)
    {
        var result = await _auth.RegisterAsync(request.Email, request.Password, cancellationToken);

        if (result.User is { } user)
        {
            SessionCookie.Append(Response, user.SessionId, SecureCookie);
            return StatusCode(StatusCodes.Status201Created, new UserResponse(user.UserId, user.Email));
        }

        // Field codes only. A taken email gets the same generic 400 as any other refusal (F1.R8).
        return result.Failure == AuthFailure.Validation
            ? ValidationProblem(new ValidationProblemDetails(ToFieldCodes(result.FieldErrors)))
            : GenericProblem(StatusCodes.Status400BadRequest, RegistrationFailedType, "Registration failed.");
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] CredentialsRequest request, CancellationToken cancellationToken)
    {
        var result = await _auth.LoginAsync(request.Email, request.Password, cancellationToken);

        if (result.User is not { } user)
        {
            // Unknown email and wrong password give this same response (F1.R8).
            return GenericProblem(StatusCodes.Status401Unauthorized, InvalidCredentialsType, "Invalid credentials.");
        }

        SessionCookie.Append(Response, user.SessionId, SecureCookie);
        return Ok(new UserResponse(user.UserId, user.Email));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _auth.LogoutAsync(Request.Cookies[SessionCookie.Name], cancellationToken);
        SessionCookie.Clear(Response, SecureCookie);
        return NoContent();
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);

        return Guid.TryParse(id, out var userId) && email is not null
            ? Ok(new UserResponse(userId, email))
            : Unauthorized();
    }

    /// <summary>
    /// A ProblemDetails with no per-request trace id, so two refusals with the same cause have the same body
    /// (F1.R8). Problem() would add a traceId that differs on every request.
    /// </summary>
    private static ObjectResult GenericProblem(int status, string type, string title) =>
        new(new ProblemDetails { Status = status, Type = type, Title = title })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };

    private static Dictionary<string, string[]> ToFieldCodes(IEnumerable<FieldError> errors) =>
        errors
            .GroupBy(error => error.Field)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Code).ToArray());
}
