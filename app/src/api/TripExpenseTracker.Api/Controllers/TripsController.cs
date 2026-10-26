using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripExpenseTracker.Api.Trips;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Api.Controllers;

/// <summary>
/// Trip list, create, get and delete (F2 Contract changes). No AllowAnonymous, so F1's default policy returns 401
/// when logged out (F2.AC15). The owner comes only from <see cref="ICurrentUser"/>, never from the request (F2.R9).
/// There is no PUT or PATCH (F2.R8).
/// </summary>
[ApiController]
[Route("api/trips")]
public sealed class TripsController : ControllerBase
{
    private const string NameField = "name";
    private const string NameRequiredCode = "name_required";
    private const string NameTooLongCode = "name_too_long";
    private const string DuplicateNameType = "duplicate-trip-name";
    private const string LimitReachedType = "trip-limit-reached";
    private const string NotFoundType = "trip-not-found";

    private readonly TripService _trips;
    private readonly ICurrentUser _currentUser;

    public TripsController(TripService trips, ICurrentUser currentUser)
    {
        _trips = trips;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var trips = await _trips.ListAsync(_currentUser.UserId, cancellationToken);
        return Ok(trips.Select(TripResponse.From).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTripRequest request, CancellationToken cancellationToken)
    {
        var result = await _trips.CreateAsync(
            _currentUser.UserId,
            request.Name,
            request.ConfirmDuplicate == true,
            cancellationToken);

        if (result.Trip is { } trip)
        {
            return CreatedAtAction(nameof(Get), new { id = trip.Id }, TripResponse.From(trip));
        }

        return result.Failure switch
        {
            TripFailure.NameRequired => NameProblem(NameRequiredCode),
            TripFailure.NameTooLong => NameProblem(NameTooLongCode),
            TripFailure.DuplicateName => GenericProblem(StatusCodes.Status409Conflict, DuplicateNameType, "A trip with this name already exists."),
            TripFailure.LimitReached => GenericProblem(StatusCodes.Status409Conflict, LimitReachedType, "You have reached the limit of 50 trips."),
            _ => throw new InvalidOperationException("Unknown trip failure."),
        };
    }

    /// <summary>
    /// A non-GUID id gets the same 404 as a missing trip (F2.AC6, F2.AC11). The id is a string so the controller can
    /// give that response; an <c>{id:guid}</c> constraint would return a bare routing 404 with no body.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var tripId))
        {
            return TripNotFound();
        }

        var trip = await _trips.GetAsync(_currentUser.UserId, tripId, cancellationToken);
        return trip is null ? TripNotFound() : Ok(TripDetailResponse.From(trip));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var tripId))
        {
            return TripNotFound();
        }

        var removed = await _trips.DeleteAsync(_currentUser.UserId, tripId, cancellationToken);
        return removed ? NoContent() : TripNotFound();
    }

    private IActionResult NameProblem(string code) =>
        ValidationProblem(new ValidationProblemDetails(
            new Dictionary<string, string[]> { [NameField] = new[] { code } }));

    private IActionResult TripNotFound() =>
        GenericProblem(StatusCodes.Status404NotFound, NotFoundType, "Trip not found.");

    /// <summary>
    /// A ProblemDetails with no per-request trace id, so two refusals with the same cause have the same body (F2.AC6).
    /// Same pattern as AuthController.
    /// </summary>
    private static ObjectResult GenericProblem(int status, string type, string title) =>
        new(new ProblemDetails { Status = status, Type = type, Title = title })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
}
