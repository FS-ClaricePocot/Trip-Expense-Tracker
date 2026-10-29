using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripExpenseTracker.Api.Payments;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.Payments;

namespace TripExpenseTracker.Api.Controllers;

/// <summary>
/// Mark as paid and delete payment (F6 Contract changes). No AllowAnonymous, so F1's default policy returns 401 when
/// logged out. The owner comes only from <see cref="ICurrentUser"/> (F6.R15). There is no PUT or PATCH (F6.R9).
/// A trip or payment the owner does not have gets the same 404, and so does a non-GUID id.
/// </summary>
[ApiController]
[Route("api/trips/{tripId}/payments")]
public sealed class PaymentsController : ControllerBase
{
    private const string NotFoundType = "not-found";
    private const string SuggestionsChangedType = "suggestions_changed";
    private const string SuggestionsChangedTitle = "The suggested payments have changed. Review them and try again.";
    private const string PayerField = "payerPersonId";
    private const string PayeeField = "payeePersonId";
    private const string AmountField = "amountCents";

    private readonly PaymentService _payments;
    private readonly ICurrentUser _currentUser;

    public PaymentsController(PaymentService payments, ICurrentUser currentUser)
    {
        _payments = payments;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Records the suggestion the client saw, in full (F6.R5, R6, R18). The 201 body is the saved payment. A payment is
    /// never recorded for a suggestion that is no longer current, which is the 409 (F6.AC9).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Record(string tripId, [FromBody] RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tripId, out var trip))
        {
            return NotFoundProblem();
        }

        var result = await _payments.MarkPaidAsync(
            _currentUser.UserId,
            trip,
            request.PayerPersonId,
            request.PayeePersonId,
            request.AmountCents,
            cancellationToken);

        return result.Payment is { } payment
            ? StatusCode(StatusCodes.Status201Created, PaymentRecordedResponse.From(payment))
            : FailureResult(result.Failure);
    }

    /// <summary>Removes one payment on the owner's trip. Foreign, missing, or not-on-this-trip is the same 404 (F6.R8, R15).</summary>
    [HttpDelete("{paymentId}")]
    public async Task<IActionResult> Delete(string tripId, string paymentId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tripId, out var trip) || !Guid.TryParse(paymentId, out var payment))
        {
            return NotFoundProblem();
        }

        var result = await _payments.DeleteAsync(_currentUser.UserId, trip, payment, cancellationToken);
        return result.Succeeded ? NoContent() : NotFoundProblem();
    }

    private IActionResult FailureResult(PaymentFailure? failure) => failure switch
    {
        PaymentFailure.PayerPayeeSame => FieldProblem(PayeeField, "payer_payee_same"),
        PaymentFailure.AmountNotPositive => FieldProblem(AmountField, "amount_not_positive"),
        PaymentFailure.PayerNotOnTrip => FieldProblem(PayerField, "payer_not_on_trip"),
        PaymentFailure.PayeeNotOnTrip => FieldProblem(PayeeField, "payee_not_on_trip"),
        PaymentFailure.SuggestionsChanged => GenericProblem(StatusCodes.Status409Conflict, SuggestionsChangedType, SuggestionsChangedTitle),
        PaymentFailure.NotFound => NotFoundProblem(),
        PaymentFailure.PaymentNotFound => NotFoundProblem(),
        _ => throw new InvalidOperationException("Unknown payment failure."),
    };

    private IActionResult FieldProblem(string field, string code) =>
        ValidationProblem(new ValidationProblemDetails(
            new Dictionary<string, string[]> { [field] = new[] { code } }));

    private IActionResult NotFoundProblem() =>
        GenericProblem(StatusCodes.Status404NotFound, NotFoundType, "Not found.");

    /// <summary>
    /// A ProblemDetails with no per-request trace id, so two refusals with the same cause have the same body.
    /// Same pattern as ExpensesController.
    /// </summary>
    private static ObjectResult GenericProblem(int status, string type, string title) =>
        new(new ProblemDetails { Status = status, Type = type, Title = title })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
}
