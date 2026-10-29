using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripExpenseTracker.Api.Balances;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.Balances;

namespace TripExpenseTracker.Api.Controllers;

/// <summary>
/// Balances and the trip summary (F5 Contract changes). Read-only: only GET actions exist (F5.R11, F5.AC15). No
/// AllowAnonymous, so F1's default policy returns 401 when logged out. The owner comes only from <see cref="ICurrentUser"/>.
/// A foreign, missing or non-GUID trip gets the same 404 (F5.R12, F5.AC12).
/// </summary>
[ApiController]
[Route("api/trips/{tripId}")]
public sealed class BalancesController : ControllerBase
{
    private const string NotFoundType = "trip-not-found";
    private const string NotFoundTitle = "Trip not found.";
    private const string InconsistentType = "balances_inconsistent";
    private const string InconsistentTitle = "Balances could not be checked. Please try again.";

    private readonly BalanceService _balances;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<BalancesController> _logger;

    public BalancesController(BalanceService balances, ICurrentUser currentUser, ILogger<BalancesController> logger)
    {
        _balances = balances;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>Each person's balance in <c>seq</c> order, or 500 <c>balances_inconsistent</c> with no amounts when the zero-sum check fails.</summary>
    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances(string tripId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tripId, out var trip))
        {
            return TripNotFound();
        }

        var result = await _balances.GetBalancesAsync(_currentUser.UserId, trip, cancellationToken);
        if (result.Succeeded)
        {
            return Ok(BalanceListResponse.From(result.Balances!));
        }

        return result.Failure switch
        {
            BalanceServiceFailure.BalancesInconsistent => Inconsistent(trip, result.PeopleCount ?? 0),
            _ => TripNotFound(),
        };
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(string tripId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tripId, out var trip))
        {
            return TripNotFound();
        }

        var result = await _balances.GetSummaryAsync(_currentUser.UserId, trip, cancellationToken);
        return result.Succeeded
            ? Ok(BalanceSummaryResponse.From(result.Summary!, result.SuggestionCount))
            : TripNotFound();
    }

    /// <summary>
    /// Logs the trip id and person count only. No amounts or names are logged (F5.AC16, CLAUDE.md logging rule).
    /// </summary>
    private IActionResult Inconsistent(Guid trip, int peopleCount)
    {
        _logger.LogError("Balance zero-sum check failed for trip {TripId} with {PeopleCount} people.", trip, peopleCount);
        return GenericProblem(StatusCodes.Status500InternalServerError, InconsistentType, InconsistentTitle);
    }

    private IActionResult TripNotFound() =>
        GenericProblem(StatusCodes.Status404NotFound, NotFoundType, NotFoundTitle);

    /// <summary>
    /// A ProblemDetails with no per-request trace id, so two refusals with the same cause have the same body.
    /// Same pattern as TripsController and ExpensesController.
    /// </summary>
    private static ObjectResult GenericProblem(int status, string type, string title) =>
        new(new ProblemDetails { Status = status, Type = type, Title = title })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
}
