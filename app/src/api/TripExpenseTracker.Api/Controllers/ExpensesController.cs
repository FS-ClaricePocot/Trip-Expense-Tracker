using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripExpenseTracker.Api.Expenses;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.Expenses;

namespace TripExpenseTracker.Api.Controllers;

/// <summary>
/// Expense list, add and delete on a trip (F4 Contract changes). No AllowAnonymous, so F1's default policy returns 401
/// when logged out. The owner comes only from <see cref="ICurrentUser"/>, never from the request (F4.R15). There is no
/// PUT or PATCH (F4.R12). A trip or expense the owner does not have gets the same 404, and so does a non-GUID id.
/// Validation codes are returned per field, the same way PeopleController returns name codes.
/// </summary>
[ApiController]
[Route("api/trips/{tripId}/expenses")]
public sealed class ExpensesController : ControllerBase
{
    private const string NotFoundType = "not-found";
    private const string NoPeopleType = "no_people_on_trip";
    private const string PeopleChangedType = "people_changed";
    private const string DescriptionField = "description";
    private const string AmountField = "amount";
    private const string PayerField = "payerPersonId";
    private const string ParticipantsField = "participantPersonIds";

    private readonly ExpenseService _expenses;
    private readonly ICurrentUser _currentUser;

    public ExpensesController(ExpenseService expenses, ICurrentUser currentUser)
    {
        _expenses = expenses;
        _currentUser = currentUser;
    }

    /// <summary>A non-GUID trip id gets the same 404 as a missing trip (F4.R15). The id is a string so the controller can give that response.</summary>
    [HttpGet]
    public async Task<IActionResult> List(string tripId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tripId, out var trip))
        {
            return NotFoundProblem();
        }

        var result = await _expenses.ListAsync(_currentUser.UserId, trip, cancellationToken);
        return result.Succeeded
            ? Ok(ExpenseListResponse.From(result))
            : NotFoundProblem();
    }

    /// <summary>The amount text goes to the service unchanged; only MoneyParser reads it (F4.R3). The 201 body is the saved expense.</summary>
    [HttpPost]
    public async Task<IActionResult> Add(string tripId, [FromBody] CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tripId, out var trip))
        {
            return NotFoundProblem();
        }

        var result = await _expenses.AddAsync(
            _currentUser.UserId,
            trip,
            request.Description,
            request.Amount,
            request.PayerPersonId,
            request.ParticipantPersonIds,
            cancellationToken);

        return result.Expense is { } expense
            ? StatusCode(StatusCodes.Status201Created, ExpenseResponse.From(expense))
            : FailureResult(result.Failure);
    }

    /// <summary>Removes one expense on the owner's trip. Foreign, missing, or not-on-this-trip is the same 404 (F4.R9, R15).</summary>
    [HttpDelete("{expenseId}")]
    public async Task<IActionResult> Delete(string tripId, string expenseId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tripId, out var trip) || !Guid.TryParse(expenseId, out var expense))
        {
            return NotFoundProblem();
        }

        var result = await _expenses.DeleteAsync(_currentUser.UserId, trip, expense, cancellationToken);
        return result.Removed ? NoContent() : NotFoundProblem();
    }

    private IActionResult FailureResult(ExpenseFailure? failure) => failure switch
    {
        ExpenseFailure.DescriptionRequired => FieldProblem(DescriptionField, "description_required"),
        ExpenseFailure.DescriptionTooLong => FieldProblem(DescriptionField, "description_too_long"),
        ExpenseFailure.AmountRequired => FieldProblem(AmountField, "amount_required"),
        ExpenseFailure.AmountInvalid => FieldProblem(AmountField, "amount_invalid"),
        ExpenseFailure.AmountTooManyDecimals => FieldProblem(AmountField, "amount_too_many_decimals"),
        ExpenseFailure.AmountNotPositive => FieldProblem(AmountField, "amount_not_positive"),
        ExpenseFailure.AmountTooLarge => FieldProblem(AmountField, "amount_too_large"),
        ExpenseFailure.PayerRequired => FieldProblem(PayerField, "payer_required"),
        ExpenseFailure.PayerNotOnTrip => FieldProblem(PayerField, "payer_not_on_trip"),
        ExpenseFailure.ParticipantsRequired => FieldProblem(ParticipantsField, "participants_required"),
        ExpenseFailure.ParticipantDuplicate => FieldProblem(ParticipantsField, "participant_duplicate"),
        ExpenseFailure.ParticipantNotOnTrip => FieldProblem(ParticipantsField, "participant_not_on_trip"),
        ExpenseFailure.NoPeopleOnTrip => GenericProblem(StatusCodes.Status409Conflict, NoPeopleType, "Add a person to the trip before adding an expense."),
        ExpenseFailure.PeopleChanged => GenericProblem(StatusCodes.Status409Conflict, PeopleChangedType, "The people on this trip changed. Try again."),
        ExpenseFailure.NotFound => NotFoundProblem(),
        _ => throw new InvalidOperationException("Unknown expense failure."),
    };

    private IActionResult FieldProblem(string field, string code) =>
        ValidationProblem(new ValidationProblemDetails(
            new Dictionary<string, string[]> { [field] = new[] { code } }));

    private IActionResult NotFoundProblem() =>
        GenericProblem(StatusCodes.Status404NotFound, NotFoundType, "Not found.");

    /// <summary>
    /// A ProblemDetails with no per-request trace id, so two refusals with the same cause have the same body.
    /// Same pattern as PeopleController and TripsController.
    /// </summary>
    private static ObjectResult GenericProblem(int status, string type, string title) =>
        new(new ProblemDetails { Status = status, Type = type, Title = title })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
}
