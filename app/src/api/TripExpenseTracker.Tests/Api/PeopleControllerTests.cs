using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripExpenseTracker.Api.Controllers;
using TripExpenseTracker.Api.People;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.People;
using TripExpenseTracker.Core.Trips;
using TripExpenseTracker.Tests.Support;

namespace TripExpenseTracker.Tests.Api;

/// <summary>
/// PeopleController with the real PeopleService over fake DAOs, usage sources and clock, and a fake ICurrentUser.
/// Covers F3.AC1, AC2, AC3, AC5, AC6, AC8, AC9, AC13 (server side) and F3.R8, F3.R10. No web host or database.
/// </summary>
public class PeopleControllerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Anna = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ben = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly FakeTripDao _trips = new();
    private readonly FakePersonDao _dao;
    private readonly Trip _annasTrip;

    public PeopleControllerTests()
    {
        _annasTrip = _trips.Seed(Anna, "Paris 2026");
        _dao = new FakePersonDao(_trips);
    }

    private string TripRoute => _annasTrip.Id.ToString();

    [Fact]
    public async Task Add_returns_201_with_the_trimmed_name_the_clock_time_and_both_flags_false()
    {
        var result = await Sut().Add(TripRoute, new AddPersonRequest("  Jo  "), CancellationToken.None);

        var created = result.Should().BeAssignableTo<ObjectResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = created.Value.Should().BeOfType<PersonResponse>().Subject;
        body.Name.Should().Be("Jo");
        body.AddedAt.Should().Be(Start);
        body.InExpense.Should().BeFalse();
        body.InPayment.Should().BeFalse();
        body.Id.Should().Be(_dao.Stored.Single().Id);
    }

    [Fact]
    public async Task Add_with_only_spaces_returns_400_name_required_and_stores_nothing()
    {
        var result = await Sut().Add(TripRoute, new AddPersonRequest("   "), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest);
        ((ValidationProblemDetails)((ObjectResult)result).Value!).Errors["name"].Should().Equal("name_required");
        _dao.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_with_101_characters_returns_400_name_too_long_and_stores_nothing()
    {
        var result = await Sut().Add(TripRoute, new AddPersonRequest(new string('a', 101)), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest);
        ((ValidationProblemDetails)((ObjectResult)result).Value!).Errors["name"].Should().Equal("name_too_long");
        _dao.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_with_a_duplicate_name_returns_409_duplicate_person_name_and_stores_nothing_new()
    {
        _dao.Seed(NewPerson("Jo"));

        var result = await Sut().Add(TripRoute, new AddPersonRequest("jo"), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status409Conflict, "duplicate-person-name");
        _dao.Stored.Should().HaveCount(1);
    }

    [Fact]
    public async Task Add_to_a_foreign_trip_is_404_and_stores_nothing()
    {
        var bens = _trips.Seed(Ben, "Ben's trip");

        var result = await Sut().Add(bens.Id.ToString(), new AddPersonRequest("Jo"), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status404NotFound, "not-found");
        _dao.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_returns_204_and_the_person_is_gone()
    {
        var jo = NewPerson("Jo");
        _dao.Seed(jo);

        var result = await Sut().Remove(TripRoute, jo.Id.ToString(), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _dao.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_of_a_person_a_source_reports_returns_409_person_in_use_and_keeps_them()
    {
        var jo = NewPerson("Jo");
        _dao.Seed(jo);
        var payments = new FakeUsageSource(PersonUsageKind.Payment);
        payments.InUse.Add(jo.Id);

        var result = await Sut(payments).Remove(TripRoute, jo.Id.ToString(), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status409Conflict, "person-in-use");
        _dao.Stored.Should().HaveCount(1);
        _dao.DeleteCalls.Should().Be(0);
    }

    [Fact]
    public async Task Remove_refused_by_the_database_foreign_key_returns_409_person_in_use_and_keeps_them()
    {
        var jo = NewPerson("Jo");
        _dao.Seed(jo);
        _dao.DeleteOutcome = PersonDeleteOutcome.InUse;

        var result = await Sut().Remove(TripRoute, jo.Id.ToString(), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status409Conflict, "person-in-use");
        _dao.Stored.Should().HaveCount(1);
    }

    [Fact]
    public async Task List_passes_the_expense_and_payment_flags_through_in_the_order_added()
    {
        var jo = NewPerson("Jo");
        var max = NewPerson("Max");
        var ann = NewPerson("Ann");
        _dao.Seed(jo);
        _dao.Seed(max);
        _dao.Seed(ann);
        var expenses = new FakeUsageSource(PersonUsageKind.Expense);
        expenses.InUse.Add(jo.Id);
        expenses.InUse.Add(ann.Id);
        var payments = new FakeUsageSource(PersonUsageKind.Payment);
        payments.InUse.Add(ann.Id);

        var result = await Sut(expenses, payments).List(TripRoute, CancellationToken.None);

        var ok = result.Should().BeAssignableTo<OkObjectResult>().Subject;
        var rows = ok.Value.Should().BeOfType<List<PersonResponse>>().Subject;
        rows.Select(r => (r.Name, r.InExpense, r.InPayment))
            .Should().Equal(("Jo", true, false), ("Max", false, false), ("Ann", true, true));
    }

    [Fact]
    public async Task List_for_a_foreign_trip_missing_trip_or_non_guid_id_gives_the_same_404()
    {
        var bens = _trips.Seed(Ben, "Ben's trip");
        var controller = Sut();

        var foreign = await controller.List(bens.Id.ToString(), CancellationToken.None);
        var missing = await controller.List(Guid.NewGuid().ToString(), CancellationToken.None);
        var nonGuid = await controller.List("not-a-guid", CancellationToken.None);

        AssertProblem(foreign, StatusCodes.Status404NotFound, "not-found");
        AssertSameProblem(foreign, missing);
        AssertSameProblem(foreign, nonGuid);
    }

    [Fact]
    public async Task Remove_of_a_person_not_on_the_trip_gives_the_same_404_as_a_foreign_trip()
    {
        var bens = _trips.Seed(Ben, "Ben's trip");
        var jo = NewPerson("Jo");
        _dao.Seed(jo);
        var controller = Sut();

        var notOnTrip = await controller.Remove(TripRoute, Guid.NewGuid().ToString(), CancellationToken.None);
        var foreignTrip = await controller.Remove(bens.Id.ToString(), jo.Id.ToString(), CancellationToken.None);
        var nonGuid = await controller.Remove(TripRoute, "not-a-guid", CancellationToken.None);

        AssertProblem(notOnTrip, StatusCodes.Status404NotFound, "not-found");
        AssertSameProblem(notOnTrip, foreignTrip);
        AssertSameProblem(notOnTrip, nonGuid);
        _dao.Stored.Should().HaveCount(1);
    }

    [Fact]
    public async Task The_owner_used_is_always_the_current_user_never_a_request_value()
    {
        // Ben is logged in and asks for Anna's trip. The owner passed to the DAO is Ben, so Anna's trip is not found.
        var ben = new FakeCurrentUser(Ben);
        var controller = new PeopleController(
            new PeopleService(_dao, _trips, [], new FakeTimeProvider(Start)),
            ben);

        var result = await controller.Add(TripRoute, new AddPersonRequest("Jo"), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status404NotFound, "not-found");
        _dao.Owners.Should().NotBeEmpty().And.OnlyContain(owner => owner == Ben);
        _dao.Stored.Should().BeEmpty();
    }

    private PeopleController Sut(params IPersonUsageSource[] sources) =>
        new(new PeopleService(_dao, _trips, sources, new FakeTimeProvider(Start)), new FakeCurrentUser(Anna));

    private Person NewPerson(string name) => new(Guid.NewGuid(), _annasTrip.Id, name, Start);

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

    private sealed class FakeTripDao : ITripDao
    {
        private readonly List<Trip> _trips = new();

        public Trip Seed(Guid owner, string name)
        {
            var trip = new Trip(Guid.NewGuid(), owner, name, Start);
            _trips.Add(trip);
            return trip;
        }

        public bool Owns(Guid owner, Guid id) => _trips.Any(t => t.Id == id && t.OwnerUserId == owner);

        public Task<Trip?> GetAsync(Guid owner, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_trips.FirstOrDefault(t => t.Id == id && t.OwnerUserId == owner));

        public Task<IReadOnlyList<Trip>> ListAsync(Guid owner, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> CountAsync(Guid owner, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> ExistsWithNameAsync(Guid owner, string name, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task InsertAsync(Trip trip, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid owner, Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task LockOwnerAsync(Guid owner, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    /// <summary>Stores people per trip. Add and delete refuse a trip the owner does not have, as the real DAO does (F3.R10).</summary>
    private sealed class FakePersonDao(FakeTripDao trips) : IPersonDao
    {
        private readonly List<Person> _stored = new();

        public IReadOnlyList<Person> Stored => _stored;

        public List<Guid> Owners { get; } = new();

        public PersonDeleteOutcome? DeleteOutcome { get; set; }

        public int DeleteCalls { get; private set; }

        public void Seed(Person person) => _stored.Add(person);

        public Task<IReadOnlyList<Person>> ListAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
        {
            Owners.Add(owner);
            IReadOnlyList<Person> rows = trips.Owns(owner, tripId)
                ? _stored.Where(p => p.TripId == tripId).ToList()
                : Array.Empty<Person>();
            return Task.FromResult(rows);
        }

        public Task<PersonAddOutcome> AddAsync(Guid owner, Guid tripId, Person person, CancellationToken cancellationToken)
        {
            Owners.Add(owner);
            if (!trips.Owns(owner, tripId))
            {
                return Task.FromResult(PersonAddOutcome.TripNotFound);
            }

            if (_stored.Any(p => p.TripId == tripId && string.Equals(p.Name, person.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(PersonAddOutcome.Duplicate);
            }

            _stored.Add(person);
            return Task.FromResult(PersonAddOutcome.Added);
        }

        public Task<PersonDeleteOutcome> DeleteAsync(Guid owner, Guid tripId, Guid personId, CancellationToken cancellationToken)
        {
            Owners.Add(owner);
            DeleteCalls++;
            if (DeleteOutcome is { } outcome)
            {
                return Task.FromResult(outcome);
            }

            if (!trips.Owns(owner, tripId))
            {
                return Task.FromResult(PersonDeleteOutcome.NotFound);
            }

            var removed = _stored.RemoveAll(p => p.Id == personId && p.TripId == tripId);
            return Task.FromResult(removed > 0 ? PersonDeleteOutcome.Removed : PersonDeleteOutcome.NotFound);
        }
    }

    private sealed class FakeUsageSource(PersonUsageKind kind) : IPersonUsageSource
    {
        public PersonUsageKind Kind { get; } = kind;

        public HashSet<Guid> InUse { get; } = new();

        public Task<IReadOnlyCollection<Guid>> GetPersonIdsInUseAsync(Guid owner, Guid tripId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(InUse.ToList());
    }
}
