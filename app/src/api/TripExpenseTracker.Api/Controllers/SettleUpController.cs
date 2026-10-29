using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripExpenseTracker.Api.Payments;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.Payments;

namespace TripExpenseTracker.Api.Controllers;

/// <summary>
/// The Settle up read (F6 Contract changes): suggestions, payments newest first, and the tiles. No AllowAnonymous, so
/// F1's default policy returns 401 when logged out. The owner comes only from <see cref="ICurrentUser"/> (F6.R15).
/// A foreign, missing or non-GUID trip gets the same 404.
/// </summary>
[ApiController]
[Route("api/trips/{tripId}/settle-up")]
public sealed class SettleUpController : ControllerBase
{
    private const string NotFoundType = "not-found";

    private readonly SettleUpService _settleUp;
    private readonly ICurrentUser _currentUser;

    public SettleUpController(SettleUpService settleUp, ICurrentUser currentUser)
    {
        _settleUp = settleUp;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string tripId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tripId, out var trip))
        {
            return NotFoundProblem();
        }

        var result = await _settleUp.GetAsync(_currentUser.UserId, trip, cancellationToken);
        return result.View is { } view
            ? Ok(SettleUpResponse.From(view))
            : NotFoundProblem();
    }

    private static ObjectResult NotFoundProblem() =>
        new(new ProblemDetails { Status = StatusCodes.Status404NotFound, Type = NotFoundType, Title = "Not found." })
        {
            StatusCode = StatusCodes.Status404NotFound,
            ContentTypes = { "application/problem+json" },
        };
}
