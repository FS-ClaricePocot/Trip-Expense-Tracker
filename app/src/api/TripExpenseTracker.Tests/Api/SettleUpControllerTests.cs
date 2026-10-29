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
/// SettleUpController with the real SettleUpService and PaymentService over fake DAOs, and a fake ICurrentUser.
/// Covers F6.AC1, AC3, AC8, AC10, AC13, AC14, AC15 (server side) and F6.R1, R7, R12, R14, R15.
/// Example: Jo owes Sam 40.00 and Alex owes Sam 10.00 (product.md, R6 Example 1 and 3). No web host or database.
/// </summary>
public class SettleUpControllerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Anna = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ben = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TripId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid EmptyTripId = Guid.Parse("44444444-4444-4444-4444-444444444444");
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

    public SettleUpControllerTests()
    {
        _trips.Add(new Trip(TripId, Anna, "Paris 2026", Start));
        _trips.Add(new Trip(EmptyTripId, Anna, "Empty trip", Start));
        _trips.Add(new Trip(Guid.NewGuid(), Ben, "Ben's trip", Start));
        _balances.Set(TripId, Anna, new BalanceSnapshot(People, Expenses));
        _balances.Set(EmptyTripId, Anna, new BalanceSnapshot(new List<BalancePerson>(), new List<BalanceExpense>()));
        _tripLock.Own(Anna, TripId);
        _tripLock.Own(Anna, EmptyTripId);
    }

    private static string Route => TripId.ToString();

    [Fact]
    public async Task Get_maps_the_suggestions_in_rule_order_and_the_tiles_before_any_payment()
    {
        var result = await Sut(Anna).Get(Route, CancellationToken.None);

        var body = ((ObjectResult)result).Value.Should().BeOfType<SettleUpResponse>().Subject;
        body.Suggestions.Should().Equal(
            new SuggestionResponse(Jo, "Jo", Sam, "Sam", 4000),
            new SuggestionResponse(Alex, "Alex", Sam, "Sam", 1000));
        body.Payments.Should().BeEmpty();
        body.PaidBackCents.Should().Be(0);
        body.OwedCents.Should().Be(5000);
        body.LeftToSettleCents.Should().Be(5000);
        body.SuggestionCount.Should().Be(2);
        body.Percentage.Should().Be(0);
        body.TotalSpentCents.Should().Be(5000);
        body.ExpenseCount.Should().Be(2);
    }

    [Fact]
    public async Task Get_after_jo_pays_shows_paid_back_owed_and_80_percent_with_the_payment_listed()
    {
        await Payments(Anna).Record(Route, new RecordPaymentRequest(Jo, Sam, 4000), CancellationToken.None);

        var result = await Sut(Anna).Get(Route, CancellationToken.None);

        var body = ((ObjectResult)result).Value.Should().BeOfType<SettleUpResponse>().Subject;
        body.PaidBackCents.Should().Be(4000);
        body.OwedCents.Should().Be(5000);
        body.LeftToSettleCents.Should().Be(1000);
        body.Percentage.Should().Be(80);
        body.SuggestionCount.Should().Be(1);
        body.Suggestions.Should().Equal(new SuggestionResponse(Alex, "Alex", Sam, "Sam", 1000));
        var payment = body.Payments.Should().ContainSingle().Subject;
        payment.PayerPersonId.Should().Be(Jo);
        payment.PayerName.Should().Be("Jo");
        payment.PayeePersonId.Should().Be(Sam);
        payment.PayeeName.Should().Be("Sam");
        payment.AmountCents.Should().Be(4000);
        payment.MarkedPaidAt.Should().Be(Start);
    }

    [Fact]
    public async Task Get_lists_payments_newest_first_and_shows_settled_when_every_balance_is_zero()
    {
        var payments = Payments(Anna);
        await payments.Record(Route, new RecordPaymentRequest(Jo, Sam, 4000), CancellationToken.None);
        await payments.Record(Route, new RecordPaymentRequest(Alex, Sam, 1000), CancellationToken.None);

        var result = await Sut(Anna).Get(Route, CancellationToken.None);

        var body = ((ObjectResult)result).Value.Should().BeOfType<SettleUpResponse>().Subject;
        body.Payments.Select(p => p.PayerPersonId).Should().Equal(Alex, Jo);
        body.Suggestions.Should().BeEmpty();
        body.SuggestionCount.Should().Be(0);
        body.LeftToSettleCents.Should().Be(0);
        body.Percentage.Should().Be(100);
    }

    [Fact]
    public async Task Get_on_a_trip_with_no_expenses_gives_a_null_percentage_and_no_suggestions()
    {
        var result = await Sut(Anna).Get(EmptyTripId.ToString(), CancellationToken.None);

        var body = ((ObjectResult)result).Value.Should().BeOfType<SettleUpResponse>().Subject;
        body.Suggestions.Should().BeEmpty();
        body.OwedCents.Should().Be(0);
        body.LeftToSettleCents.Should().Be(0);
        body.SuggestionCount.Should().Be(0);
        body.Percentage.Should().BeNull();
    }

    [Fact]
    public async Task Get_for_a_foreign_missing_or_non_guid_trip_gives_the_same_404()
    {
        var controller = Sut(Ben);

        var foreign = await controller.Get(Route, CancellationToken.None);
        var missing = await controller.Get(Guid.NewGuid().ToString(), CancellationToken.None);
        var nonGuid = await controller.Get("not-a-guid", CancellationToken.None);

        AssertProblem(foreign, StatusCodes.Status404NotFound, "not-found");
        AssertSameProblem(foreign, missing);
        AssertSameProblem(foreign, nonGuid);
    }

    /// <summary>The settle-up read over the same fakes the payments controller writes to.</summary>
    private SettleUpController Sut(Guid user) =>
        new(new SettleUpService(_trips, _balances, _payments), new FakeSettleCurrentUser(user));

    /// <summary>The payments controller, so a test can record a payment and then read the settle-up view.</summary>
    private PaymentsController Payments(Guid user) =>
        new(
            new PaymentService(_payments, _balances, _trips, _tripLock, new FakeSettleTransaction(), _time),
            new FakeSettleCurrentUser(user));

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
