using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripExpenseTracker.Api.People;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.People;

namespace TripExpenseTracker.Api.Controllers;

/// <summary>
/// People list, add and remove on a trip (F3 Contract changes). No AllowAnonymous, so F1's default policy returns 401
/// when logged out. The owner comes only from <see cref="ICurrentUser"/>, never from the request (F3.R10). There is
/// no PUT or PATCH (F3.R8). A trip or person the owner does not have gets the same 404 (F3.R10).
/// </summary>
[ApiController]
[Route("api/trips/{tripId}/people")]
public sealed class PeopleController : ControllerBase
{
    private const string NameField = "name";
    private const string NameRequiredCode = "name_required";
    private const string NameTooLongCode = "name_too_long";
    private const string DuplicateNameType = "duplicate-person-name";
    private const string InUseType = "person-in-use";
    private const string NotFoundType = "not-found";

    private readonly PeopleService _people;
    private readonly ICurrentUser _currentUser;

    public PeopleController(PeopleService people, ICurrentUser currentUser)
    {
        _people = people;
        _currentUser = currentUser;
    }

    /// <summary>A non-GUID trip id gets the same 404 as a missing trip (F3.R10). The id is a string so the controller can give that response.</summary>
    [HttpGet]
    public async Task<IActionResult> List(string tripId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tripId, out var trip))
        {
            return NotFoundProblem();
        }

        var result = await _people.ListAsync(_currentUser.UserId, trip, cancellationToken);
        return result.People is { } people
            ? Ok(people.Select(PersonResponse.From).ToList())
            : NotFoundProblem();
    }

    [HttpPost]
    public async Task<IActionResult> Add(string tripId, [FromBody] AddPersonRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tripId, out var trip))
        {
            return NotFoundProblem();
        }

        var result = await _people.AddAsync(_currentUser.UserId, trip, request.Name, cancellationToken);
        if (result.Person is { } person)
        {
            return StatusCode(StatusCodes.Status201Created, PersonResponse.Added(person));
        }

        return result.Failure switch
        {
            PersonFailure.NameRequired => NameProblem(NameRequiredCode),
            PersonFailure.NameTooLong => NameProblem(NameTooLongCode),
            PersonFailure.DuplicateName => GenericProblem(StatusCodes.Status409Conflict, DuplicateNameType, "A person with this name is already on this trip."),
            PersonFailure.NotFound => NotFoundProblem(),
            _ => throw new InvalidOperationException("Unknown person failure."),
        };
    }

    /// <summary>
    /// A removal the database refuses (an expense or payment still uses the person) is 409 and the person stays
    /// (F3.R7, F3.AC8). A foreign or missing trip, or a person not on that trip, is the same 404 (F3.R10).
    /// </summary>
    [HttpDelete("{personId}")]
    public async Task<IActionResult> Remove(string tripId, string personId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tripId, out var trip) || !Guid.TryParse(personId, out var person))
        {
            return NotFoundProblem();
        }

        var result = await _people.RemoveAsync(_currentUser.UserId, trip, person, cancellationToken);
        if (result.Removed)
        {
            return NoContent();
        }

        return result.Failure switch
        {
            PersonFailure.InUse => GenericProblem(StatusCodes.Status409Conflict, InUseType, "This person is in an expense or payment and cannot be removed."),
            PersonFailure.NotFound => NotFoundProblem(),
            _ => throw new InvalidOperationException("Unknown person failure."),
        };
    }

    private IActionResult NameProblem(string code) =>
        ValidationProblem(new ValidationProblemDetails(
            new Dictionary<string, string[]> { [NameField] = new[] { code } }));

    private IActionResult NotFoundProblem() =>
        GenericProblem(StatusCodes.Status404NotFound, NotFoundType, "Not found.");

    /// <summary>
    /// A ProblemDetails with no per-request trace id, so two refusals with the same cause have the same body.
    /// Same pattern as TripsController.
    /// </summary>
    private static ObjectResult GenericProblem(int status, string type, string title) =>
        new(new ProblemDetails { Status = status, Type = type, Title = title })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
}
