using FluentAssertions;
using TripExpenseTracker.Core.People;
using TripExpenseTracker.Core.Trips;
using TripExpenseTracker.Tests.Support;

namespace TripExpenseTracker.Tests.People;

/// <summary>People name rules, list, add and remove (F3.R1 to R3, R5 to R10, F3.AC3 to AC5, AC8, AC9), with fake DAOs, usage sources and clock.</summary>
public class PeopleServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Anna = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ben = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TripId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly FakePersonDao _people = new();
    private readonly FakeTripDao _trips = new();
    private readonly FakeTimeProvider _time = new(Start);
    private readonly FakeUsageSource _expenses = new(PersonUsageKind.Expense);
    private readonly FakeUsageSource _payments = new(PersonUsageKind.Payment);

    private PeopleService Sut(params IPersonUsageSource[] sources) =>
        new(_people, _trips, sources, _time);

    public PeopleServiceTests()
    {
        _trips.Add(new Trip(TripId, Anna, "Paris 2026", Start));
    }

    [Fact]
    public async Task Add_saves_the_trimmed_name_with_the_trip_and_the_clock_time()
    {
        var result = await Sut().AddAsync(Anna, TripId, "  Jo  ", CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var saved = _people.Stored.Should().ContainSingle().Subject;
        saved.Name.Should().Be("Jo");
        saved.TripId.Should().Be(TripId);
        saved.Id.Should().NotBe(Guid.Empty);
        result.Person.Should().Be(saved);
    }

    [Fact]
    public async Task Add_sets_added_at_to_the_clock_time()
    {
        await Sut().AddAsync(Anna, TripId, "Jo", CancellationToken.None);

        _people.Stored.Should().ContainSingle().Which.AddedAt.Should().Be(Start);
    }

    [Fact]
    public async Task Add_with_only_spaces_is_refused_and_inserts_nothing()
    {
        var result = await Sut().AddAsync(Anna, TripId, "   ", CancellationToken.None);

        result.Failure.Should().Be(PersonFailure.NameRequired);
        _people.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_with_100_characters_succeeds()
    {
        var result = await Sut().AddAsync(Anna, TripId, new string('a', 100), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Add_with_101_characters_is_refused_and_inserts_nothing()
    {
        var result = await Sut().AddAsync(Anna, TripId, new string('a', 101), CancellationToken.None);

        result.Failure.Should().Be(PersonFailure.NameTooLong);
        _people.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_with_100_emoji_succeeds()
    {
        var name = string.Concat(Enumerable.Repeat("\U0001F600", 100));

        var result = await Sut().AddAsync(Anna, TripId, name, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Add_with_101_emoji_is_refused()
    {
        var name = string.Concat(Enumerable.Repeat("\U0001F600", 101));

        var result = await Sut().AddAsync(Anna, TripId, name, CancellationToken.None);

        result.Failure.Should().Be(PersonFailure.NameTooLong);
        _people.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_reports_duplicate_name_when_the_dao_refuses()
    {
        _people.AddOutcome = PersonAddOutcome.Duplicate;

        var result = await Sut().AddAsync(Anna, TripId, "Sam", CancellationToken.None);

        result.Failure.Should().Be(PersonFailure.DuplicateName);
    }

    [Fact]
    public async Task Add_to_a_trip_that_is_not_found_gives_not_found_and_inserts_nothing()
    {
        _people.AddOutcome = PersonAddOutcome.TripNotFound;

        var result = await Sut().AddAsync(Anna, Guid.NewGuid(), "Sam", CancellationToken.None);

        result.Failure.Should().Be(PersonFailure.NotFound);
        _people.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task List_returns_people_in_dao_order_with_no_flags_when_no_sources_are_registered()
    {
        _people.Seed(new Person(Guid.NewGuid(), TripId, "Sam", Start));
        _people.Seed(new Person(Guid.NewGuid(), TripId, "Riley", Start));

        var result = await Sut().ListAsync(Anna, TripId, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.People!.Select(p => p.Person.Name).Should().Equal("Sam", "Riley");
        result.People!.Should().OnlyContain(p => !p.InExpense && !p.InPayment);
    }

    [Fact]
    public async Task List_with_no_sources_registered_marks_nobody_in_use()
    {
        var sam = new Person(Guid.NewGuid(), TripId, "Sam", Start);
        _people.Seed(sam);

        var result = await Sut().ListAsync(Anna, TripId, CancellationToken.None);

        result.People!.Should().ContainSingle().Which.Should().Be(new PersonWithUsage(sam, false, false));
    }

    [Fact]
    public async Task List_flags_a_person_in_an_expense_as_in_expense()
    {
        var sam = new Person(Guid.NewGuid(), TripId, "Sam", Start);
        _people.Seed(sam);
        _expenses.InUse.Add(sam.Id);

        var result = await Sut(_expenses, _payments).ListAsync(Anna, TripId, CancellationToken.None);

        var row = result.People!.Should().ContainSingle().Subject;
        row.InExpense.Should().BeTrue();
        row.InPayment.Should().BeFalse();
    }

    [Fact]
    public async Task List_flags_a_person_in_a_payment_as_in_payment()
    {
        var riley = new Person(Guid.NewGuid(), TripId, "Riley", Start);
        _people.Seed(riley);
        _payments.InUse.Add(riley.Id);

        var result = await Sut(_expenses, _payments).ListAsync(Anna, TripId, CancellationToken.None);

        var row = result.People!.Should().ContainSingle().Subject;
        row.InExpense.Should().BeFalse();
        row.InPayment.Should().BeTrue();
    }

    [Fact]
    public async Task List_allows_both_flags_on_the_same_person()
    {
        var sam = new Person(Guid.NewGuid(), TripId, "Sam", Start);
        _people.Seed(sam);
        _expenses.InUse.Add(sam.Id);
        _payments.InUse.Add(sam.Id);

        var result = await Sut(_expenses, _payments).ListAsync(Anna, TripId, CancellationToken.None);

        var row = result.People!.Should().ContainSingle().Subject;
        row.InExpense.Should().BeTrue();
        row.InPayment.Should().BeTrue();
    }

    [Fact]
    public async Task List_for_a_trip_that_is_not_found_gives_not_found()
    {
        var result = await Sut(_expenses).ListAsync(Anna, Guid.NewGuid(), CancellationToken.None);

        result.Failure.Should().Be(PersonFailure.NotFound);
        result.People.Should().BeNull();
    }

    [Fact]
    public async Task List_for_another_users_trip_gives_not_found_and_does_not_ask_the_sources()
    {
        var result = await Sut(_expenses).ListAsync(Ben, TripId, CancellationToken.None);

        result.Failure.Should().Be(PersonFailure.NotFound);
        _expenses.Calls.Should().Be(0);
    }

    /// <summary>Every usage source gets the owner, so its query can filter on owner_user_id (project rules).</summary>
    [Fact]
    public async Task List_and_remove_pass_the_owner_to_every_usage_source()
    {
        var sam = new Person(Guid.NewGuid(), TripId, "Sam", Start);
        _people.Seed(sam);

        await Sut(_expenses, _payments).ListAsync(Anna, TripId, CancellationToken.None);
        await Sut(_expenses, _payments).RemoveAsync(Anna, TripId, sam.Id, CancellationToken.None);

        _expenses.Owners.Should().Equal(Anna, Anna);
        _payments.Owners.Should().Equal(Anna, Anna);
    }

    [Fact]
    public async Task Remove_is_refused_with_in_use_when_a_source_reports_the_person_and_the_dao_is_not_called()
    {
        var sam = new Person(Guid.NewGuid(), TripId, "Sam", Start);
        _people.Seed(sam);
        _expenses.InUse.Add(sam.Id);

        var result = await Sut(_expenses, _payments).RemoveAsync(Anna, TripId, sam.Id, CancellationToken.None);

        result.Failure.Should().Be(PersonFailure.InUse);
        result.Removed.Should().BeFalse();
        _people.DeleteCalls.Should().Be(0);
        _people.Stored.Should().ContainSingle();
    }

    [Fact]
    public async Task Remove_is_refused_with_in_use_when_a_payment_source_reports_the_person()
    {
        var riley = new Person(Guid.NewGuid(), TripId, "Riley", Start);
        _people.Seed(riley);
        _payments.InUse.Add(riley.Id);

        var result = await Sut(_expenses, _payments).RemoveAsync(Anna, TripId, riley.Id, CancellationToken.None);

        result.Failure.Should().Be(PersonFailure.InUse);
        _people.DeleteCalls.Should().Be(0);
    }

    [Fact]
    public async Task Remove_goes_through_when_no_source_reports_the_person()
    {
        var sam = new Person(Guid.NewGuid(), TripId, "Sam", Start);
        _people.Seed(sam);

        var result = await Sut(_expenses, _payments).RemoveAsync(Anna, TripId, sam.Id, CancellationToken.None);

        result.Removed.Should().BeTrue();
        _people.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_of_the_last_person_on_a_trip_is_allowed()
    {
        var sam = new Person(Guid.NewGuid(), TripId, "Sam", Start);
        _people.Seed(sam);

        var result = await Sut().RemoveAsync(Anna, TripId, sam.Id, CancellationToken.None);

        result.Removed.Should().BeTrue();
        _people.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_becomes_in_use_when_the_dao_foreign_key_backstop_refuses()
    {
        var sam = new Person(Guid.NewGuid(), TripId, "Sam", Start);
        _people.Seed(sam);
        _people.DeleteOutcome = PersonDeleteOutcome.InUse;

        var result = await Sut(_expenses).RemoveAsync(Anna, TripId, sam.Id, CancellationToken.None);

        result.Failure.Should().Be(PersonFailure.InUse);
        _people.Stored.Should().ContainSingle();
    }

    [Fact]
    public async Task Remove_of_a_person_not_on_the_trip_gives_not_found()
    {
        _people.DeleteOutcome = PersonDeleteOutcome.NotFound;

        var result = await Sut().RemoveAsync(Anna, TripId, Guid.NewGuid(), CancellationToken.None);

        result.Failure.Should().Be(PersonFailure.NotFound);
    }

    [Fact]
    public async Task Remove_for_a_trip_that_is_not_found_gives_not_found_and_does_not_ask_the_sources()
    {
        var result = await Sut(_expenses).RemoveAsync(Ben, TripId, Guid.NewGuid(), CancellationToken.None);

        result.Failure.Should().Be(PersonFailure.NotFound);
        _expenses.Calls.Should().Be(0);
        _people.DeleteCalls.Should().Be(0);
    }

    private sealed class FakePersonDao : IPersonDao
    {
        private readonly List<Person> _stored = new();

        public IReadOnlyList<Person> Stored => _stored;

        public PersonAddOutcome? AddOutcome { get; set; }

        public PersonDeleteOutcome? DeleteOutcome { get; set; }

        public int DeleteCalls { get; private set; }

        public void Seed(Person person) => _stored.Add(person);

        public Task<IReadOnlyList<Person>> ListAsync(Guid owner, Guid tripId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Person>>(_stored.Where(p => p.TripId == tripId).ToList());

        public Task<PersonAddOutcome> AddAsync(Guid owner, Guid tripId, Person person, CancellationToken cancellationToken)
        {
            if (AddOutcome is { } outcome)
            {
                return Task.FromResult(outcome);
            }

            _stored.Add(person);
            return Task.FromResult(PersonAddOutcome.Added);
        }

        public Task<PersonDeleteOutcome> DeleteAsync(Guid owner, Guid tripId, Guid personId, CancellationToken cancellationToken)
        {
            DeleteCalls++;
            if (DeleteOutcome is { } outcome)
            {
                return Task.FromResult(outcome);
            }

            var removed = _stored.RemoveAll(p => p.Id == personId && p.TripId == tripId);
            return Task.FromResult(removed > 0 ? PersonDeleteOutcome.Removed : PersonDeleteOutcome.NotFound);
        }
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

    private sealed class FakeUsageSource : IPersonUsageSource
    {
        public FakeUsageSource(PersonUsageKind kind) => Kind = kind;

        public PersonUsageKind Kind { get; }

        public HashSet<Guid> InUse { get; } = new();

        public int Calls { get; private set; }

        public List<Guid> Owners { get; } = new();

        public Task<IReadOnlyCollection<Guid>> GetPersonIdsInUseAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
        {
            Calls++;
            Owners.Add(owner);
            return Task.FromResult<IReadOnlyCollection<Guid>>(InUse.ToList());
        }
    }
}
