using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripExpenseTracker.Api.Controllers;
using TripExpenseTracker.Api.Payments;
using TripExpenseTracker.Core.Balances;
using TripExpenseTracker.Core.Payments;
using TripExpenseTracker.Core.Trips;
using TripExpenseTracker.Tests.Support;

namespace TripExpenseTracker.Tests.Api;

/// <summary>
/// PaymentsController with the real PaymentService over fake DAOs, lock, transaction and clock, and a fake ICurrentUser.
/// Covers F6.AC2, AC5, AC6, AC9, AC13 (server side) and F6.R5, R6, R8, R15, R18. No web host or database.
/// Example: Jo owes Sam 40.00 and Alex owes Sam 10.00 (product.md, R6 Example 1).
/// </summary>
public class PaymentsControllerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Anna = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ben = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TripId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Jo = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Sam = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid Alex = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static readonly IReadOnlyList<BalancePerson> People = new List<BalancePerson>
    {
        new(Jo, "Jo", 0),
        new(Sam, "Sam", 1),
        new(Alex, "Alex", 2),
    };

    /// <summary>Sam paid 40.00 for Jo, and 10.00 for Alex.</summary>
    private static readonly IReadOnlyList<BalanceExpense> Expenses = new List<BalanceExpense>
    {
        new(4000, Sam, new List<(Guid PersonId, long Seq)> { (Jo, 0) }),
        new(1000, Sam, new List<(Guid PersonId, long Seq)> { (Alex, 2) }),
    };

    private readonly FakeSettleTrips _trips = new();
    private readonly FakeSettleBalances _balances = new();
    private readonly FakeSettlePayments _payments = new(new Dictionary<Guid, string> { [Jo] = "Jo", [Sam] = "Sam", [Alex] = "Alex" });
    private readonly FakeTripLock _tripLock = new();
    private readonly FakeTimeProvider _time = new(Start);

    public PaymentsControllerTests()
    {
        _trips.Add(new Trip(TripId, Anna, "Paris 2026", Start));
        _trips.Add(new Trip(Guid.NewGuid(), Ben, "Ben's trip", Start));
        _balances.Set(TripId, Anna, new BalanceSnapshot(People, Expenses));
        _tripLock.Own(Anna, TripId);
    }

    private static string Route => TripId.ToString();

    [Fact]
    public async Task Record_returns_201_with_the_saved_payment_in_integer_cents()
    {
        var result = await Sut(Anna).Record(Route, new RecordPaymentRequest(Jo, Sam, 4000), CancellationToken.None);

        var created = result.Should().BeAssignableTo<ObjectResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = created.Value.Should().BeOfType<PaymentRecordedResponse>().Subject;
        body.Id.Should().NotBe(Guid.Empty);
        body.PayerPersonId.Should().Be(Jo);
        body.PayeePersonId.Should().Be(Sam);
        body.AmountCents.Should().Be(4000);
        body.MarkedPaidAt.Should().Be(Start);
        _payments.RowCount.Should().Be(1);
    }

    [Fact]
    public async Task Record_refuses_an_amount_that_is_not_a_current_suggestion_with_409_and_stores_nothing()
    {
        var result = await Sut(Anna).Record(Route, new RecordPaymentRequest(Jo, Sam, 3999), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status409Conflict, "suggestions_changed");
        _payments.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Record_with_payer_equal_to_payee_returns_400_payer_payee_same()
    {
        var result = await Sut(Anna).Record(Route, new RecordPaymentRequest(Jo, Jo, 4000), CancellationToken.None);

        AssertValidation(result, "payeePersonId", "payer_payee_same");
        _payments.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Record_with_zero_amount_returns_400_amount_not_positive()
    {
        var result = await Sut(Anna).Record(Route, new RecordPaymentRequest(Jo, Sam, 0), CancellationToken.None);

        AssertValidation(result, "amountCents", "amount_not_positive");
        _payments.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Record_with_a_payer_not_on_the_trip_returns_400_payer_not_on_trip()
    {
        var result = await Sut(Anna).Record(Route, new RecordPaymentRequest(Guid.NewGuid(), Sam, 4000), CancellationToken.None);

        AssertValidation(result, "payerPersonId", "payer_not_on_trip");
    }

    [Fact]
    public async Task Record_with_a_payee_not_on_the_trip_returns_400_payee_not_on_trip()
    {
        var result = await Sut(Anna).Record(Route, new RecordPaymentRequest(Jo, Guid.NewGuid(), 4000), CancellationToken.None);

        AssertValidation(result, "payeePersonId", "payee_not_on_trip");
    }

    /// <summary>Another user's trip gives the same not-found as a missing one, and nothing is recorded (F6.AC13).</summary>
    [Fact]
    public async Task Record_on_a_foreign_trip_gives_the_same_404_as_a_missing_trip()
    {
        var controller = Sut(Ben);

        var foreign = await controller.Record(Route, new RecordPaymentRequest(Jo, Sam, 4000), CancellationToken.None);
        var missing = await controller.Record(Guid.NewGuid().ToString(), new RecordPaymentRequest(Jo, Sam, 4000), CancellationToken.None);
        var nonGuid = await controller.Record("not-a-guid", new RecordPaymentRequest(Jo, Sam, 4000), CancellationToken.None);

        AssertProblem(foreign, StatusCodes.Status404NotFound, "not-found");
        AssertSameProblem(foreign, missing);
        AssertSameProblem(foreign, nonGuid);
        _payments.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_returns_204_and_removes_the_payment()
    {
        var controller = Sut(Anna);
        var created = await controller.Record(Route, new RecordPaymentRequest(Jo, Sam, 4000), CancellationToken.None);
        var id = ((PaymentRecordedResponse)((ObjectResult)created).Value!).Id;

        var result = await controller.Delete(Route, id.ToString(), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _payments.RowCount.Should().Be(0);
    }

    /// <summary>Deleting on another user's trip gives the same not-found as a missing one, and removes nothing (F6.AC13).</summary>
    [Fact]
    public async Task Delete_of_a_missing_payment_foreign_trip_or_non_guid_gives_the_same_404_and_removes_nothing()
    {
        var anna = Sut(Anna);
        var created = await anna.Record(Route, new RecordPaymentRequest(Jo, Sam, 4000), CancellationToken.None);
        var id = ((PaymentRecordedResponse)((ObjectResult)created).Value!).Id;

        var missing = await anna.Delete(Route, Guid.NewGuid().ToString(), CancellationToken.None);
        var nonGuid = await anna.Delete(Route, "not-a-guid", CancellationToken.None);
        var foreignTrip = await Sut(Ben).Delete(Route, id.ToString(), CancellationToken.None);

        AssertProblem(missing, StatusCodes.Status404NotFound, "not-found");
        AssertSameProblem(missing, nonGuid);
        AssertSameProblem(missing, foreignTrip);
        _payments.RowCount.Should().Be(1);
    }

    private PaymentsController Sut(Guid user) =>
        new(
            new PaymentService(_payments, _balances, _trips, _tripLock, new FakeSettleTransaction(), _time),
            new FakeSettleCurrentUser(user));

    private static void AssertValidation(IActionResult result, string field, string code)
    {
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ((ValidationProblemDetails)objectResult.Value!).Errors[field].Should().Equal(code);
    }

    private static void AssertProblem(IActionResult result, int status, string type)
    {
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(status);
        ((ProblemDetails)objectResult.Value!).Type.Should().Be(type);
    }

    /// <summary>Two refusals with the same cause must have the same status, type and title (no per-request detail).</summary>
    private static void AssertSameProblem(IActionResult expected, IActionResult actual)
    {
        var a = (ObjectResult)expected;
        var b = (ObjectResult)actual;
        b.StatusCode.Should().Be(a.StatusCode);
        ((ProblemDetails)b.Value!).Type.Should().Be(((ProblemDetails)a.Value!).Type);
        ((ProblemDetails)b.Value!).Title.Should().Be(((ProblemDetails)a.Value!).Title);
    }
}
