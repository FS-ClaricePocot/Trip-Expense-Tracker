using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripExpenseTracker.Api.Controllers;
using TripExpenseTracker.Api.Trips;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.Trips;
using TripExpenseTracker.Tests.Support;

namespace TripExpenseTracker.Tests.Api;

/// <summary>
/// TripsController with the real TripService over fake DAO, transaction and clock, and a fake ICurrentUser.
/// Covers F2.AC1 to AC8, AC10, AC11 (server side) and F2.R8, F2.R9. No web host or database.
/// </summary>
public class TripsControllerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Anna = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ben = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly FakeTripDao _dao = new();
    private readonly FakeCurrentUser _currentUser = new(Anna);
    private readonly TripsController _sut;

    public TripsControllerTests()
    {
        var service = new TripService(_dao, new FakeTransaction(), new FakeTimeProvider(Start));
        _sut = new TripsController(service, _currentUser);
    }

    [Fact]
    public async Task Create_returns_201_with_the_get_route_and_the_trip_body()
    {
        var result = await _sut.Create(new CreateTripRequest("  Paris 2026  ", null), CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(TripsController.Get));
        created.RouteValues!["id"].Should().Be(_dao.Trips.Single().Id);
        var body = created.Value.Should().BeOfType<TripResponse>().Subject;
        body.Name.Should().Be("Paris 2026");
        body.CreatedAt.Should().Be(Start);
    }

    [Fact]
    public async Task Create_with_only_spaces_returns_400_name_required_and_inserts_nothing()
    {
        var result = await _sut.Create(new CreateTripRequest("   ", null), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest);
        ((ValidationProblemDetails)((ObjectResult)result).Value!).Errors["name"].Should().Equal("name_required");
        _dao.Trips.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_with_101_characters_returns_400_name_too_long_and_inserts_nothing()
    {
        var result = await _sut.Create(new CreateTripRequest(new string('a', 101), null), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest);
        ((ValidationProblemDetails)((ObjectResult)result).Value!).Errors["name"].Should().Equal("name_too_long");
        _dao.Trips.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_with_100_characters_returns_201()
    {
        var result = await _sut.Create(new CreateTripRequest(new string('a', 100), null), CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Create_with_a_duplicate_name_returns_409_duplicate_trip_name_and_inserts_nothing()
    {
        _dao.Seed(Anna, "Paris 2026");

        var result = await _sut.Create(new CreateTripRequest("paris 2026", null), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status409Conflict, "duplicate-trip-name");
        _dao.CountFor(Anna).Should().Be(1);
    }

    [Fact]
    public async Task Create_with_a_duplicate_name_and_confirm_true_passes_the_confirmation_and_returns_201()
    {
        _dao.Seed(Anna, "Paris 2026");

        var result = await _sut.Create(new CreateTripRequest("Paris 2026", true), CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
        _dao.CountFor(Anna).Should().Be(2);
    }

    [Fact]
    public async Task Create_with_confirm_false_or_null_does_not_confirm_a_duplicate()
    {
        _dao.Seed(Anna, "Paris 2026");

        var withFalse = await _sut.Create(new CreateTripRequest("Paris 2026", false), CancellationToken.None);
        var withNull = await _sut.Create(new CreateTripRequest("Paris 2026", null), CancellationToken.None);

        AssertProblem(withFalse, StatusCodes.Status409Conflict, "duplicate-trip-name");
        AssertProblem(withNull, StatusCodes.Status409Conflict, "duplicate-trip-name");
        _dao.CountFor(Anna).Should().Be(1);
    }

    [Fact]
    public async Task Create_at_50_trips_returns_409_trip_limit_reached_and_inserts_nothing()
    {
        _dao.Seed(Anna, 50);

        var result = await _sut.Create(new CreateTripRequest("One more", null), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status409Conflict, "trip-limit-reached");
        _dao.CountFor(Anna).Should().Be(50);
    }

    [Fact]
    public async Task List_returns_only_the_current_users_trips()
    {
        _dao.Seed(Anna, "Paris 2026");
        _dao.Seed(Ben, "Someone else");
        _dao.Seed(Anna, "Rome 2027");

        var result = await _sut.List(CancellationToken.None);

        var items = ((IEnumerable<TripResponse>)((OkObjectResult)result).Value!).ToList();
        items.Select(item => item.Name).Should().Equal("Paris 2026", "Rome 2027");
    }

    [Fact]
    public async Task Get_returns_the_name_and_three_zero_counts()
    {
        var trip = _dao.Seed(Anna, "Paris 2026");

        var result = await _sut.Get(trip.Id.ToString(), CancellationToken.None);

        var body = ((OkObjectResult)result).Value.Should().BeOfType<TripDetailResponse>().Subject;
        body.Id.Should().Be(trip.Id);
        body.Name.Should().Be("Paris 2026");
        body.PeopleCount.Should().Be(0);
        body.ExpenseCount.Should().Be(0);
        body.PaymentCount.Should().Be(0);
    }

    [Fact]
    public async Task Get_of_a_trip_owned_by_someone_else_returns_the_same_404_as_a_missing_trip()
    {
        var bensTrip = _dao.Seed(Ben, "Ben's trip");

        var notOwned = await _sut.Get(bensTrip.Id.ToString(), CancellationToken.None);
        var missing = await _sut.Get(Guid.NewGuid().ToString(), CancellationToken.None);

        AssertProblem(notOwned, StatusCodes.Status404NotFound, "trip-not-found");
        AssertSameProblem(notOwned, missing);
    }

    [Fact]
    public async Task Get_with_a_non_guid_id_returns_the_same_404_as_a_missing_trip()
    {
        var missing = await _sut.Get(Guid.NewGuid().ToString(), CancellationToken.None);
        var notGuid = await _sut.Get("not-a-guid", CancellationToken.None);

        AssertProblem(notGuid, StatusCodes.Status404NotFound, "trip-not-found");
        AssertSameProblem(notGuid, missing);
    }

    [Fact]
    public async Task Delete_returns_204_and_removes_the_trip()
    {
        var trip = _dao.Seed(Anna, "Paris 2026");

        var result = await _sut.Delete(trip.Id.ToString(), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _dao.Trips.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_of_another_users_trip_returns_404_and_keeps_the_trip()
    {
        var bensTrip = _dao.Seed(Ben, "Ben's trip");

        var result = await _sut.Delete(bensTrip.Id.ToString(), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status404NotFound, "trip-not-found");
        _dao.Trips.Should().ContainSingle(trip => trip.Id == bensTrip.Id);
    }

    [Fact]
    public async Task Delete_with_a_non_guid_id_returns_404()
    {
        var result = await _sut.Delete("not-a-guid", CancellationToken.None);

        AssertProblem(result, StatusCodes.Status404NotFound, "trip-not-found");
    }

    [Fact]
    public async Task The_owner_used_is_always_the_current_user_never_a_request_value()
    {
        // Ben's trip id is in the URL, but the owner passed to the DAO is Anna, so Ben's trip is not found.
        var bensTrip = _dao.Seed(Ben, "Ben's trip");

        await _sut.Get(bensTrip.Id.ToString(), CancellationToken.None);
        await _sut.Create(new CreateTripRequest("Mine", null), CancellationToken.None);

        _dao.Calls.Should().NotBeEmpty().And.OnlyContain(call => call.Owner == Anna);
    }

    private static void AssertProblem(IActionResult result, int status, string? type = null)
    {
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(status);
        if (type is not null)
        {
            ((ProblemDetails)objectResult.Value!).Type.Should().Be(type);
        }
    }

    private static void AssertSameProblem(IActionResult expected, IActionResult actual)
    {
        var first = (ObjectResult)expected;
        var second = (ObjectResult)actual;
        second.StatusCode.Should().Be(first.StatusCode);
        var a = (ProblemDetails)first.Value!;
        var b = (ProblemDetails)second.Value!;
        b.Type.Should().Be(a.Type);
        b.Title.Should().Be(a.Title);
        b.Status.Should().Be(a.Status);
        b.Instance.Should().Be(a.Instance);
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeTransaction : IAuthTransaction
    {
        public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
            await work(cancellationToken);
    }

    private sealed class FakeTripDao : ITripDao
    {
        public List<Trip> Trips { get; } = new();
        public List<(string Method, Guid Owner)> Calls { get; } = new();

        public Trip Seed(Guid owner, string name)
        {
            var trip = new Trip(Guid.NewGuid(), owner, name, Start);
            Trips.Add(trip);
            return trip;
        }

        public void Seed(Guid owner, int count)
        {
            for (var i = 0; i < count; i++)
            {
                Seed(owner, $"Trip {i}");
            }
        }

        public int CountFor(Guid owner) => Trips.Count(trip => trip.OwnerUserId == owner);

        public Task<IReadOnlyList<Trip>> ListAsync(Guid owner, CancellationToken cancellationToken)
        {
            Calls.Add(("list", owner));
            IReadOnlyList<Trip> items = Trips.Where(trip => trip.OwnerUserId == owner).ToList();
            return Task.FromResult(items);
        }

        public Task<Trip?> GetAsync(Guid owner, Guid id, CancellationToken cancellationToken)
        {
            Calls.Add(("get", owner));
            return Task.FromResult(Trips.SingleOrDefault(trip => trip.OwnerUserId == owner && trip.Id == id));
        }

        public Task<int> CountAsync(Guid owner, CancellationToken cancellationToken)
        {
            Calls.Add(("count", owner));
            return Task.FromResult(CountFor(owner));
        }

        public Task<bool> ExistsWithNameAsync(Guid owner, string name, CancellationToken cancellationToken)
        {
            Calls.Add(("exists", owner));
            return Task.FromResult(Trips.Any(trip =>
                trip.OwnerUserId == owner && string.Equals(trip.Name, name, StringComparison.OrdinalIgnoreCase)));
        }

        public Task InsertAsync(Trip trip, CancellationToken cancellationToken)
        {
            Calls.Add(("insert", trip.OwnerUserId));
            Trips.Add(trip);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Guid owner, Guid id, CancellationToken cancellationToken)
        {
            Calls.Add(("delete", owner));
            var removed = Trips.RemoveAll(trip => trip.OwnerUserId == owner && trip.Id == id);
            return Task.FromResult(removed > 0);
        }

        public Task LockOwnerAsync(Guid owner, CancellationToken cancellationToken)
        {
            Calls.Add(("lock", owner));
            return Task.CompletedTask;
        }
    }
}
