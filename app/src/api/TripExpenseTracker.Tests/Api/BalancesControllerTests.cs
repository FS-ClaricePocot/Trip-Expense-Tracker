using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using TripExpenseTracker.Api.Balances;
using TripExpenseTracker.Api.Controllers;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.Balances;
using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Tests.Api;

/// <summary>
/// BalancesController with the real BalanceService over fake DAOs and a fake ICurrentUser, and a capturing logger.
/// Covers F5.AC1, AC2, AC9, AC10, AC12, AC15, AC16 (controller side) and F5.R11, R12. No web host or database.
/// </summary>
public class BalancesControllerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Anna = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ben = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Sam = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Alex = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Jo = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static readonly IReadOnlyList<BalancePerson> People =
    [
        new BalancePerson(Sam, "Sam", 1),
        new BalancePerson(Alex, "Alex", 2),
        new BalancePerson(Jo, "Jo", 3),
    ];

    private readonly FakeTripDao _trips = new();
    private readonly FakeBalanceDao _balances = new();
    private readonly CapturingLogger _logger = new();
    private readonly Trip _annasTrip;
    private readonly Trip _bensTrip;

    public BalancesControllerTests()
    {
        _annasTrip = new Trip(Guid.NewGuid(), Anna, "Paris 2026", Start);
        _bensTrip = new Trip(Guid.NewGuid(), Ben, "Ben's trip", Start);
        _trips.Add(_annasTrip);
        _trips.Add(_bensTrip);
        _balances.TripId = _annasTrip.Id;
        _balances.Snapshot = new BalanceSnapshot(People, ExampleOneExpenses());
    }

    private string TripRoute => _annasTrip.Id.ToString();

    private BalancesController Sut(Guid? user = null, Func<IEnumerable<PersonBalance>, BalanceFailure?>? verify = null) =>
        new(new BalanceService(_balances, _trips, verify), new FakeCurrentUser(user ?? Anna), _logger);

    [Fact]
    public async Task Balances_map_example_1_in_seq_order_with_status_and_zero_total()
    {
        var result = await Sut().GetBalances(TripRoute, CancellationToken.None);

        var body = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<BalanceListResponse>().Subject;
        body.TotalCents.Should().Be(0);
        body.People.Select(p => p.Name).Should().Equal("Sam", "Alex", "Jo");
        body.People.Select(p => p.BalanceCents).Should().Equal(5000L, -1000L, -4000L);
        body.People.Select(p => p.Status).Should().Equal("owed", "owes", "owes");
        body.People[0].Id.Should().Be(Sam);
    }

    [Fact]
    public async Task A_person_in_no_expense_is_listed_as_settled()
    {
        _balances.Snapshot = new BalanceSnapshot(People, []);

        var result = await Sut().GetBalances(TripRoute, CancellationToken.None);

        var body = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<BalanceListResponse>().Subject;
        body.People.Should().HaveCount(3);
        body.People.Select(p => p.Status).Should().AllBeEquivalentTo("settled");
        body.People.Select(p => p.BalanceCents).Should().AllBeEquivalentTo(0L);
    }

    [Fact]
    public async Task Summary_maps_example_1_to_total_spent_and_left_to_settle()
    {
        var result = await Sut().GetSummary(TripRoute, CancellationToken.None);

        var body = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<BalanceSummaryResponse>().Subject;
        body.TotalSpentCents.Should().Be(12000);
        body.LeftToSettleCents.Should().Be(5000);
    }

    [Fact]
    public async Task Foreign_missing_and_non_guid_trips_get_the_same_404_on_both_endpoints()
    {
        var routes = new[] { _bensTrip.Id.ToString(), Guid.NewGuid().ToString(), "not-a-guid" };

        foreach (var route in routes)
        {
            AssertNotFound(await Sut().GetBalances(route, CancellationToken.None));
            AssertNotFound(await Sut().GetSummary(route, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Inconsistent_check_returns_500_with_no_amounts_or_names_and_logs_the_trip_and_person_count_only()
    {
        var failing = Sut(verify: _ => BalanceFailure.BalancesInconsistent);

        var result = await failing.GetBalances(TripRoute, CancellationToken.None);

        var obj = result.Should().BeAssignableTo<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var problem = obj.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Type.Should().Be("balances_inconsistent");

        var visible = string.Join(" ", new[] { problem.Type, problem.Title, problem.Detail }
            .Concat(problem.Extensions.Values.Select(v => v?.ToString())));
        foreach (var secret in new[] { "Sam", "Alex", "Jo", "5000", "1000", "4000", "12000" })
        {
            visible.Should().NotContain(secret);
        }

        _logger.Entries.Should().ContainSingle();
        var line = _logger.Entries[0].Message;
        line.Should().Be($"Balance zero-sum check failed for trip {_annasTrip.Id} with 3 people.");
        foreach (var secret in new[] { "Sam", "Alex", "Jo", "5000", "1000", "4000", "12000" })
        {
            line.Should().NotContain(secret);
        }
    }

    [Fact]
    public async Task The_owner_is_always_the_current_user_so_another_users_trip_is_not_found()
    {
        var result = await Sut(user: Ben).GetBalances(TripRoute, CancellationToken.None);

        AssertNotFound(result);
    }

    [Fact]
    public void The_controller_has_no_anonymous_access_and_only_get_actions()
    {
        typeof(BalancesController).GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Should().BeEmpty();

        var actions = typeof(BalancesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), true).Length > 0)
            .ToList();
        actions.Should().HaveCount(2);
        foreach (var action in actions)
        {
            action.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Should().BeEmpty();
            action.GetCustomAttributes(typeof(HttpGetAttribute), true).Should().NotBeEmpty();
        }
    }

    private static void AssertNotFound(IActionResult result)
    {
        var obj = result.Should().BeAssignableTo<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var problem = obj.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Type.Should().Be("trip-not-found");
        problem.Title.Should().Be("Trip not found.");
    }

    /// <summary>Example 1: Sam paid 90.00 and Alex paid 30.00, both shared by Sam, Alex and Jo.</summary>
    private static IReadOnlyList<BalanceExpense> ExampleOneExpenses() =>
    [
        new BalanceExpense(9000, Sam, [(Sam, 1), (Alex, 2), (Jo, 3)]),
        new BalanceExpense(3000, Alex, [(Sam, 1), (Alex, 2), (Jo, 3)]),
    ];

    /// <summary>Like the real DAO: the snapshot only for the owner's trip, empty lists otherwise.</summary>
    private sealed class FakeBalanceDao : IBalanceDao
    {
        public BalanceSnapshot Snapshot { get; set; } = new([], []);

        public Guid TripId { get; set; }

        public Task<BalanceSnapshot> GetSnapshotAsync(Guid owner, Guid tripId, CancellationToken cancellationToken) =>
            Task.FromResult(owner == Anna && tripId == TripId ? Snapshot : new BalanceSnapshot([], []));
    }

    private sealed class FakeTripDao : ITripDao
    {
        private readonly List<Trip> _trips = new();

        public void Add(Trip trip) => _trips.Add(trip);

        public Task<Trip?> GetAsync(Guid owner, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_trips.FirstOrDefault(t => t.Id == id && t.OwnerUserId == owner));

        public Task<IReadOnlyList<Trip>> ListAsync(Guid owner, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> CountAsync(Guid owner, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> ExistsWithNameAsync(Guid owner, string name, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task InsertAsync(Trip trip, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid owner, Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task LockOwnerAsync(Guid owner, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class CapturingLogger : ILogger<BalancesController>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}
