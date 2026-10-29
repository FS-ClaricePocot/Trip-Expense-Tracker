using FluentAssertions;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.Expenses;
using TripExpenseTracker.Core.People;
using TripExpenseTracker.Core.Trips;
using TripExpenseTracker.Tests.Support;

namespace TripExpenseTracker.Tests.Expenses;

/// <summary>Expense add, list and delete rules (F4.R1, R4 to R9, R13 to R15; F4.AC1, AC8, AC10, AC11, AC14), with fake DAOs, transaction and clock.</summary>
public class ExpenseServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Anna = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ben = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TripId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherTripId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid BensTripId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private readonly FakeTripDao _trips = new();
    private readonly FakePersonDao _people = new();
    private readonly FakeExpenseDao _expenses;
    private readonly FakeTransaction _transaction = new();
    private readonly FakeTimeProvider _time = new(Start);
    private readonly FakeTripLock _tripLock = new();
    private readonly ExpenseService _sut;

    public ExpenseServiceTests()
    {
        _expenses = new FakeExpenseDao(_people);
        _sut = new ExpenseService(_expenses, _people, _trips, _transaction, _time, _tripLock);
        _trips.Add(new Trip(TripId, Anna, "Paris 2026", Start));
        _trips.Add(new Trip(OtherTripId, Anna, "Rome 2026", Start));
        _trips.Add(new Trip(BensTripId, Ben, "Ben's trip", Start));
        _tripLock.Own(Anna, TripId);
        _tripLock.Own(Anna, OtherTripId);
        _tripLock.Own(Ben, BensTripId);
    }

    /// <summary>Add takes the trip lock inside its transaction before it writes (F6.R18).</summary>
    [Fact]
    public async Task Add_takes_the_trip_lock_inside_the_transaction_before_writing()
    {
        var sam = _people.Add(Anna, TripId, "Sam");

        var result = await _sut.AddAsync(Anna, TripId, "Taxi", "12.00", sam, new[] { sam }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _tripLock.CallCount.Should().Be(1);
        _transaction.RunCount.Should().Be(1);
        _expenses.RowCount.Should().Be(1);
    }

    /// <summary>A refused lock stops the add before any row is written, so the lock gates the insert (F6.R15, F6.R18).</summary>
    [Fact]
    public async Task Add_to_a_trip_the_lock_does_not_own_writes_nothing()
    {
        var sam = _people.Add(Anna, TripId, "Sam");

        var result = await _sut.AddAsync(Anna, BensTripId, "Taxi", "12.00", sam, new[] { sam }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        _tripLock.CallCount.Should().Be(1);
        _expenses.RowCount.Should().Be(0);
    }

    /// <summary>Delete takes the trip lock inside a transaction, and removes the expense when the lock is held (F6.R18).</summary>
    [Fact]
    public async Task Delete_takes_the_trip_lock_and_removes_the_expense()
    {
        var sam = _people.Add(Anna, TripId, "Sam");
        var added = await _sut.AddAsync(Anna, TripId, "Taxi", "12.00", sam, new[] { sam }, CancellationToken.None);
        var before = _tripLock.CallCount;
        var runsBefore = _transaction.RunCount;

        var result = await _sut.DeleteAsync(Anna, TripId, added.Expense!.Id, CancellationToken.None);

        result.Removed.Should().BeTrue();
        _tripLock.CallCount.Should().Be(before + 1);
        _transaction.RunCount.Should().Be(runsBefore + 1);
        _expenses.RowCount.Should().Be(0);
    }

    /// <summary>Delete on a trip the lock does not own is not found, and removes nothing (F4.R15, F6.R15).</summary>
    [Fact]
    public async Task Delete_on_a_trip_the_lock_does_not_own_is_not_found_and_removes_nothing()
    {
        var sam = _people.Add(Anna, TripId, "Sam");
        var added = await _sut.AddAsync(Anna, TripId, "Taxi", "12.00", sam, new[] { sam }, CancellationToken.None);

        var result = await _sut.DeleteAsync(Anna, BensTripId, added.Expense!.Id, CancellationToken.None);

        result.Removed.Should().BeFalse();
        result.Failure.Should().Be(ExpenseFailure.NotFound);
        _expenses.RowCount.Should().Be(1);
    }

    [Fact]
    public async Task Dinner_for_90_00_with_sam_alex_and_jo_gives_30_00_each()
    {
        var sam = _people.Add(Anna, TripId, "Sam");
        var alex = _people.Add(Anna, TripId, "Alex");
        var jo = _people.Add(Anna, TripId, "Jo");

        var result = await _sut.AddAsync(Anna, TripId, "Dinner", "90.00", sam, new[] { sam, alex, jo }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Expense!.AmountCents.Should().Be(9000);
        result.Expense.Payer.Name.Should().Be("Sam");
        result.Expense.Participants.Should().HaveCount(3);
        result.Expense.Participants.Select(p => p.ShareCents).Should().AllBeEquivalentTo(3000L);
    }

    [Fact]
    public async Task Payer_who_is_not_a_participant_is_accepted_and_gets_no_share()
    {
        var sam = _people.Add(Anna, TripId, "Sam");
        var alex = _people.Add(Anna, TripId, "Alex");
        var jo = _people.Add(Anna, TripId, "Jo");

        var result = await _sut.AddAsync(Anna, TripId, "Taxi", "60.00", sam, new[] { alex, jo }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Expense!.Participants.Select(p => p.PersonId).Should().NotContain(sam);
        result.Expense.Participants.Select(p => p.ShareCents).Should().AllBeEquivalentTo(3000L);
    }

    [Fact]
    public async Task Add_is_refused_when_the_trip_has_no_people_and_nothing_is_saved()
    {
        var result = await _sut.AddAsync(Anna, TripId, "Dinner", "10.00", Guid.NewGuid(), new[] { Guid.NewGuid() }, CancellationToken.None);

        result.Failure.Should().Be(ExpenseFailure.NoPeopleOnTrip);
        _expenses.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Add_refuses_a_payer_from_another_trip()
    {
        var sam = _people.Add(Anna, TripId, "Sam");
        var lee = _people.Add(Anna, OtherTripId, "Lee");

        var result = await _sut.AddAsync(Anna, TripId, "Dinner", "10.00", lee, new[] { sam }, CancellationToken.None);

        result.Failure.Should().Be(ExpenseFailure.PayerNotOnTrip);
        _expenses.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Add_refuses_a_participant_from_another_trip()
    {
        var sam = _people.Add(Anna, TripId, "Sam");
        var lee = _people.Add(Anna, OtherTripId, "Lee");

        var result = await _sut.AddAsync(Anna, TripId, "Dinner", "10.00", sam, new[] { sam, lee }, CancellationToken.None);

        result.Failure.Should().Be(ExpenseFailure.ParticipantNotOnTrip);
        _expenses.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Added_time_comes_from_the_clock_and_the_list_is_newest_first()
    {
        var sam = _people.Add(Anna, TripId, "Sam");
        await _sut.AddAsync(Anna, TripId, "X", "5.00", sam, new[] { sam }, CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(1));
        var second = await _sut.AddAsync(Anna, TripId, "Y", "5.00", sam, new[] { sam }, CancellationToken.None);

        second.Expense!.CreatedAt.Should().Be(Start.AddMinutes(1));

        var list = await _sut.ListAsync(Anna, TripId, CancellationToken.None);

        list.Expenses.Select(e => e.Description).Should().Equal("Y", "X");
        list.Expenses[0].CreatedAt.Should().Be(Start.AddMinutes(1));
        list.Expenses[1].CreatedAt.Should().Be(Start);
    }

    [Fact]
    public async Task List_total_equals_the_sum_of_every_amount()
    {
        var sam = _people.Add(Anna, TripId, "Sam");
        await _sut.AddAsync(Anna, TripId, "Coffee", "10.00", sam, new[] { sam }, CancellationToken.None);
        await _sut.AddAsync(Anna, TripId, "Bus", "5.25", sam, new[] { sam }, CancellationToken.None);

        var list = await _sut.ListAsync(Anna, TripId, CancellationToken.None);

        list.Succeeded.Should().BeTrue();
        list.Total.Should().Be(1525);
        list.Count.Should().Be(2);
    }

    [Fact]
    public async Task Add_is_refused_when_it_would_push_the_trip_total_past_long_max()
    {
        var sam = _people.Add(Anna, TripId, "Sam");
        var first = await _sut.AddAsync(Anna, TripId, "Huge", "92233720368547758.00", sam, new[] { sam }, CancellationToken.None);
        first.Succeeded.Should().BeTrue();

        var second = await _sut.AddAsync(Anna, TripId, "Over", "0.10", sam, new[] { sam }, CancellationToken.None);

        second.Failure.Should().Be(ExpenseFailure.AmountTooLarge);
        _expenses.RowCount.Should().Be(1);
    }

    [Theory]
    [InlineData("   ", "1.00", ExpenseFailure.DescriptionRequired)]
    [InlineData("Dinner", "", ExpenseFailure.AmountRequired)]
    [InlineData("Dinner", "1.234", ExpenseFailure.AmountTooManyDecimals)]
    [InlineData("Dinner", "abc", ExpenseFailure.AmountInvalid)]
    [InlineData("Dinner", "0.00", ExpenseFailure.AmountNotPositive)]
    public async Task Bad_text_or_amount_is_refused_before_the_trip_is_read(string description, string amount, ExpenseFailure expected)
    {
        var result = await _sut.AddAsync(Anna, TripId, description, amount, Guid.NewGuid(), new[] { Guid.NewGuid() }, CancellationToken.None);

        result.Failure.Should().Be(expected);
        _transaction.RunCount.Should().Be(0);
    }

    [Fact]
    public async Task Add_refuses_a_missing_payer_and_missing_participants()
    {
        var noPayer = await _sut.AddAsync(Anna, TripId, "Dinner", "10.00", null, new[] { Guid.NewGuid() }, CancellationToken.None);
        var noParticipants = await _sut.AddAsync(Anna, TripId, "Dinner", "10.00", Guid.NewGuid(), Array.Empty<Guid>(), CancellationToken.None);

        noPayer.Failure.Should().Be(ExpenseFailure.PayerRequired);
        noParticipants.Failure.Should().Be(ExpenseFailure.ParticipantsRequired);
    }

    [Fact]
    public async Task Add_refuses_a_participant_listed_twice()
    {
        var sam = _people.Add(Anna, TripId, "Sam");

        var result = await _sut.AddAsync(Anna, TripId, "Dinner", "10.00", sam, new[] { sam, sam }, CancellationToken.None);

        result.Failure.Should().Be(ExpenseFailure.ParticipantDuplicate);
    }

    [Fact]
    public async Task Add_reports_people_changed_when_the_save_finds_a_person_gone_and_keeps_nothing()
    {
        var sam = _people.Add(Anna, TripId, "Sam");
        _expenses.NextInsertOutcome = ExpenseInsertOutcome.PersonMissing;

        var result = await _sut.AddAsync(Anna, TripId, "Dinner", "10.00", sam, new[] { sam }, CancellationToken.None);

        result.Failure.Should().Be(ExpenseFailure.PeopleChanged);
        _expenses.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_of_an_unknown_id_returns_not_found()
    {
        var result = await _sut.DeleteAsync(Anna, TripId, Guid.NewGuid(), CancellationToken.None);

        result.Failure.Should().Be(ExpenseFailure.NotFound);
    }

    [Fact]
    public async Task Delete_removes_the_expense()
    {
        var sam = _people.Add(Anna, TripId, "Sam");
        var added = await _sut.AddAsync(Anna, TripId, "Dinner", "10.00", sam, new[] { sam }, CancellationToken.None);

        var result = await _sut.DeleteAsync(Anna, TripId, added.Expense!.Id, CancellationToken.None);

        result.Removed.Should().BeTrue();
        (await _sut.ListAsync(Anna, TripId, CancellationToken.None)).Count.Should().Be(0);
    }

    [Fact]
    public async Task A_trip_owned_by_someone_else_is_not_found_for_add_list_or_delete()
    {
        var sam = _people.Add(Ben, BensTripId, "Sam");

        var add = await _sut.AddAsync(Anna, BensTripId, "Dinner", "10.00", sam, new[] { sam }, CancellationToken.None);
        var list = await _sut.ListAsync(Anna, BensTripId, CancellationToken.None);
        var delete = await _sut.DeleteAsync(Anna, BensTripId, Guid.NewGuid(), CancellationToken.None);

        add.Failure.Should().Be(ExpenseFailure.NotFound);
        list.Failure.Should().Be(ExpenseFailure.NotFound);
        delete.Failure.Should().Be(ExpenseFailure.NotFound);
    }

    /// <summary>Fake of the trip DAO. Every read is filtered by owner, as the real DAO must do (F4.R15).</summary>
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

    /// <summary>Fake of the person DAO. People are kept in the order added, which is their seq (F3.R5).</summary>
    private sealed class FakePersonDao : IPersonDao
    {
        private readonly List<(Guid Owner, Person Person)> _rows = new();

        public Guid Add(Guid owner, Guid tripId, string name)
        {
            var person = new Person(Guid.NewGuid(), tripId, name, Start);
            _rows.Add((owner, person));
            return person.Id;
        }

        public Task<IReadOnlyList<Person>> ListAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
        {
            IReadOnlyList<Person> people = _rows
                .Where(r => r.Owner == owner && r.Person.TripId == tripId)
                .Select(r => r.Person)
                .ToList();
            return Task.FromResult(people);
        }

        /// <summary>The name and seq (index in the trip's list) of a person on the trip.</summary>
        public ExpensePerson Lookup(Guid tripId, Guid personId)
        {
            var onTrip = _rows.Select(r => r.Person).Where(p => p.TripId == tripId).ToList();
            var index = onTrip.FindIndex(p => p.Id == personId);
            return new ExpensePerson(personId, onTrip[index].Name, index);
        }

        public Task<PersonAddOutcome> AddAsync(Guid owner, Guid tripId, Person person, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PersonDeleteOutcome> DeleteAsync(Guid owner, Guid tripId, Guid personId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    /// <summary>Fake of the expense DAO. Keeps rows with an insertion seq, and can be told to report a person gone.</summary>
    private sealed class FakeExpenseDao(FakePersonDao people) : IExpenseDao
    {
        private readonly List<(Guid Owner, Expense Expense, IReadOnlyList<Guid> Participants, long Seq)> _rows = new();
        private long _nextSeq = 1;

        public ExpenseInsertOutcome NextInsertOutcome { get; set; } = ExpenseInsertOutcome.Inserted;

        public int RowCount => _rows.Count;

        public Task<IReadOnlyList<StoredExpense>> ListAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
        {
            IReadOnlyList<StoredExpense> stored = _rows
                .Where(r => r.Owner == owner && r.Expense.TripId == tripId)
                .Select(r => new StoredExpense(
                    r.Expense,
                    r.Seq,
                    people.Lookup(tripId, r.Expense.PayerPersonId),
                    r.Participants.Select(id => people.Lookup(tripId, id)).ToList()))
                .ToList();
            return Task.FromResult(stored);
        }

        public Task<ExpenseInsertOutcome> InsertAsync(Guid owner, Expense expense, IReadOnlyList<Guid> participantPersonIds, CancellationToken cancellationToken)
        {
            if (NextInsertOutcome == ExpenseInsertOutcome.Inserted)
            {
                _rows.Add((owner, expense, participantPersonIds, _nextSeq++));
            }

            return Task.FromResult(NextInsertOutcome);
        }

        public Task<bool> DeleteAsync(Guid owner, Guid tripId, Guid expenseId, CancellationToken cancellationToken)
        {
            var removed = _rows.RemoveAll(r => r.Owner == owner && r.Expense.TripId == tripId && r.Expense.Id == expenseId);
            return Task.FromResult(removed > 0);
        }
    }

    private sealed class FakeTransaction : IAuthTransaction
    {
        public int RunCount { get; private set; }

        public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
        {
            RunCount++;
            return await work(cancellationToken);
        }
    }
}
