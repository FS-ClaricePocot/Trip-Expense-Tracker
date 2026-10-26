using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TripExpenseTracker.Core.Auth;

namespace TripExpenseTracker.Api.Auth;

/// <summary>
/// Reads the user id that <see cref="SessionAuthenticationHandler"/> put on the principal (F1.R9).
/// The fallback policy means every endpoint that reads this has an authenticated user. If the claim is
/// missing anyway, this throws rather than returning an empty id.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid UserId
    {
        get
        {
            var value = _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id)
                ? id
                : throw new InvalidOperationException("No logged-in user on this request.");
        }
    }
}
