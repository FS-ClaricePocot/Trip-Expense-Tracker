using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripExpenseTracker.Api.Controllers;
using TripExpenseTracker.Api.Expenses;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.Expenses;
using TripExpenseTracker.Core.People;
using TripExpenseTracker.Core.Trips;
using TripExpenseTracker.Tests.Support;

namespace TripExpenseTracker.Tests.Api;

/// <summary>
/// ExpensesController with the real ExpenseService over fake DAOs, transaction and clock, and a fake ICurrentUser.
/// Covers F4.AC1, AC2 (response shape), AC3 to AC7, AC10, AC12, AC14, AC16, AC17 (server side) and F4.R12, R15.
/// No web host or database.
/// </summary>
public class ExpensesControllerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Anna = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ben = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly FakeTripDao _trips = new();
    private readonly FakePersonDao _people = new();
    private readonly FakeExpenseDao _expenses;
    private readonly FakeTransaction _transaction = new();
    private readonly FakeTimeProvider _time = new(Start);
    private readonly FakeTripLock _tripLock = new();
    private readonly Trip _annasTrip;

    public ExpensesControllerTests()
    {
        _expenses = new FakeExpenseDao(_people);
        _annasTrip = new Trip(Guid.NewGuid(), Anna, "Paris 2026", Start);
        _trips.Add(_annasTrip);
        _tripLock.Own(Anna, _annasTrip.Id);
        _trips.Add(new Trip(Guid.NewGuid(), Ben, "Ben's trip", Start));
    }

    private string TripRoute => _annasTrip.Id.ToString();

    [Fact]
    public async Task Add_returns_201_with_integer_cents_and_each_participant_share()
    {
        var sam = _people.Add(Anna, _annasTrip.Id, "Sam");
        var alex = _people.Add(Anna, _annasTrip.Id, "Alex");
        var jo = _people.Add(Anna, _annasTrip.Id, "Jo");

        var result = await Sut().Add(TripRoute, Request("Dinner", "90.00", sam, sam, alex, jo), CancellationToken.None);

        var created = result.Should().BeAssignableTo<ObjectResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = created.Value.Should().BeOfType<ExpenseResponse>().Subject;
        body.Description.Should().Be("Dinner");
        body.AmountCents.Should().Be(9000);
        body.Payer.Should().Be(new ExpensePersonResponse(sam, "Sam"));
        body.ParticipantCount.Should().Be(3);
        body.Participants.Select(p => p.ShareCents).Should().Equal(3000, 3000, 3000);
        body.Participants.Select(p => p.Name).Should().Equal("Sam", "Alex", "Jo");
        body.CreatedAt.Should().Be(Start);
        _expenses.RowCount.Should().Be(1);
    }

    [Fact]
    public async Task Add_gives_the_leftover_cents_to_the_first_participants_in_seq_order()
    {
        var sam = _people.Add(Anna, _annasTrip.Id, "Sam");
        var alex = _people.Add(Anna, _annasTrip.Id, "Alex");
        var jo = _people.Add(Anna, _annasTrip.Id, "Jo");

        var result = await Sut().Add(TripRoute, Request("Taxi", "10.00", jo, sam, alex, jo), CancellationToken.None);

        var body = ((ObjectResult)result).Value.Should().BeOfType<ExpenseResponse>().Subject;
        body.Participants.Select(p => p.ShareCents).Should().Equal(334, 333, 333);
        body.Participants.Sum(p => p.ShareCents).Should().Be(body.AmountCents);
    }

    [Theory]
    [InlineData("", "description_required")]
    [InlineData("   ", "description_required")]
    public async Task Blank_description_returns_400_description_required_and_stores_nothing(string description, string code)
    {
        var sam = _people.Add(Anna, _annasTrip.Id, "Sam");

        var result = await Sut().Add(TripRoute, Request(description, "5.00", sam, sam), CancellationToken.None);

        AssertValidation(result, "description", code);
        _expenses.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Description_of_101_characters_returns_400_description_too_long()
    {
        var sam = _people.Add(Anna, _annasTrip.Id, "Sam");

        var result = await Sut().Add(TripRoute, Request(new string('d', 101), "5.00", sam, sam), CancellationToken.None);

        AssertValidation(result, "description", "description_too_long");
        _expenses.RowCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null, "amount_required")]
    [InlineData("", "amount_required")]
    [InlineData("abc", "amount_invalid")]
    [InlineData(".5", "amount_invalid")]
    [InlineData("1,000", "amount_invalid")]
    [InlineData("1.234", "amount_too_many_decimals")]
    [InlineData("0", "amount_not_positive")]
    [InlineData("0.00", "amount_not_positive")]
    [InlineData("99999999999999999999", "amount_too_large")]
    public async Task Each_amount_problem_returns_400_with_its_code_and_stores_nothing(string? amount, string code)
    {
        var sam = _people.Add(Anna, _annasTrip.Id, "Sam");

        var result = await Sut().Add(TripRoute, Request("Lunch", amount, sam, sam), CancellationToken.None);

        AssertValidation(result, "amount", code);
        _expenses.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Missing_payer_returns_400_payer_required()
    {
        var sam = _people.Add(Anna, _annasTrip.Id, "Sam");

        var result = await Sut().Add(TripRoute, Request("Lunch", "5.00", null, sam), CancellationToken.None);

        AssertValidation(result, "payerPersonId", "payer_required");
        _expenses.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Payer_from_another_trip_returns_400_payer_not_on_trip()
    {
        var sam = _people.Add(Anna, _annasTrip.Id, "Sam");
        var outsider = _people.Add(Ben, Guid.NewGuid(), "Outsider");

        var result = await Sut().Add(TripRoute, Request("Lunch", "5.00", outsider, sam), CancellationToken.None);

        AssertValidation(result, "payerPersonId", "payer_not_on_trip");
        _expenses.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task No_participants_returns_400_participants_required()
    {
        var sam = _people.Add(Anna, _annasTrip.Id, "Sam");

        var result = await Sut().Add(TripRoute, Request("Lunch", "5.00", sam), CancellationToken.None);

        AssertValidation(result, "participantPersonIds", "participants_required");
    }

    [Fact]
    public async Task Same_participant_twice_returns_400_participant_duplicate()
    {
        var sam = _people.Add(Anna, _annasTrip.Id, "Sam");

        var result = await Sut().Add(TripRoute, Request("Lunch", "5.00", sam, sam, sam), CancellationToken.None);

        AssertValidation(result, "participantPersonIds", "participant_duplicate");
    }

    [Fact]
    public async Task Participant_from_another_trip_returns_400_participant_not_on_trip()
    {
        var sam = _people.Add(Anna, _annasTrip.Id, "Sam");
        var outsider = _people.Add(Ben, Guid.NewGuid(), "Outsider");

        var result = await Sut().Add(TripRoute, Request("Lunch", "5.00", sam, sam, outsider), CancellationToken.None);

        AssertValidation(result, "participantPersonIds", "participant_not_on_trip");
        _expenses.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Trip_with_no_people_returns_409_no_people_on_trip()
    {
        var someone = Guid.NewGuid();

        var result = await Sut().Add(TripRoute, Request("Lunch", "5.00", someone, someone), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status409Conflict, "no_people_on_trip");
        _expenses.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Person_removed_before_the_save_returns_409_people_changed_and_stores_nothing()
    {
        var sam = _people.Add(Anna, _annasTrip.Id, "Sam");
        _expenses.NextInsertOutcome = ExpenseInsertOutcome.PersonMissing;

        var result = await Sut().Add(TripRoute, Request("Lunch", "5.00", sam, sam), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status409Conflict, "people_changed");
        _expenses.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Add_to_a_foreign_trip_is_404_and_stores_nothing()
    {
        var bens = _trips.AddTrip(Ben, "Ben's other trip");
        var bensPerson = _people.Add(Ben, bens, "Ben");

        var result = await Sut().Add(bens.ToString(), Request("Lunch", "5.00", bensPerson, bensPerson), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status404NotFound, "not-found");
        _expenses.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Add_with_a_non_guid_trip_is_the_same_404()
    {
        var sam = _people.Add(Anna, _annasTrip.Id, "Sam");

        var result = await Sut().Add("not-a-guid", Request("Lunch", "5.00", sam, sam), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status404NotFound, "not-found");
    }

    [Fact]
    public async Task List_returns_total_count_and_each_rows_participant_count_newest_first()
    {
        var sam = _people.Add(Anna, _annasTrip.Id, "Sam");
        var alex = _people.Add(Anna, _annasTrip.Id, "Alex");
        var controller = Sut();
        await controller.Add(TripRoute, Request("First", "10.00", sam, sam, alex), CancellationToken.None);
        await controller.Add(TripRoute, Request("Second", "2.50", alex, sam), CancellationToken.None);

        var result = await controller.List(TripRoute, CancellationToken.None);

        var body = ((OkObjectResult)result).Value.Should().BeOfType<ExpenseListResponse>().Subject;
        body.Total.Should().Be(1250);
        body.Count.Should().Be(2);
        body.Expenses.Select(e => e.Description).Should().Equal("Second", "First");
        body.Expenses.Select(e => e.ParticipantCount).Should().Equal(1, 2);
    }

    [Fact]
    public async Task List_of_a_trip_with_no_expenses_is_zero_and_empty()
    {
        var result = await Sut().List(TripRoute, CancellationToken.None);

        var body = ((OkObjectResult)result).Value.Should().BeOfType<ExpenseListResponse>().Subject;
        body.Total.Should().Be(0);
        body.Count.Should().Be(0);
        body.Expenses.Should().BeEmpty();
    }

    [Fact]
    public void Response_rows_have_no_icon_or_per_person_amount_field()
    {
        var rowProperties = typeof(ExpenseResponse).GetProperties().Select(p => p.Name).ToList();

        rowProperties.Should().NotContain(name => name.Contains("Icon", StringComparison.OrdinalIgnoreCase));
        rowProperties.Should().NotContain(name => name.Contains("PerPerson", StringComparison.OrdinalIgnoreCase));
        rowProperties.Should().Contain("ParticipantCount");
    }

    [Fact]
    public async Task List_of_a_foreign_trip_is_404_the_same_as_missing_and_non_guid()
    {
        var bens = _trips.AddTrip(Ben, "Ben's other trip");
        var controller = Sut();

        var foreign = await controller.List(bens.ToString(), CancellationToken.None);
        var missing = await controller.List(Guid.NewGuid().ToString(), CancellationToken.None);
        var nonGuid = await controller.List("not-a-guid", CancellationToken.None);

        AssertProblem(foreign, StatusCodes.Status404NotFound, "not-found");
        AssertSameProblem(foreign, missing);
        AssertSameProblem(foreign, nonGuid);
    }

    [Fact]
    public async Task Delete_returns_204_and_the_expense_is_gone()
    {
        var sam = _people.Add(Anna, _annasTrip.Id, "Sam");
        var controller = Sut();
        var added = await controller.Add(TripRoute, Request("Lunch", "5.00", sam, sam), CancellationToken.None);
        var id = ((ExpenseResponse)((ObjectResult)added).Value!).Id;

        var result = await controller.Delete(TripRoute, id.ToString(), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _expenses.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_of_a_missing_expense_is_404_the_same_as_a_non_guid_id_and_a_foreign_trip()
    {
        var sam = _people.Add(Anna, _annasTrip.Id, "Sam");
        var bens = _trips.AddTrip(Ben, "Ben's other trip");
        var controller = Sut();
        var added = await controller.Add(TripRoute, Request("Lunch", "5.00", sam, sam), CancellationToken.None);
        var id = ((ExpenseResponse)((ObjectResult)added).Value!).Id;

        var missing = await controller.Delete(TripRoute, Guid.NewGuid().ToString(), CancellationToken.None);
        var nonGuid = await controller.Delete(TripRoute, "not-a-guid", CancellationToken.None);
        var foreignTrip = await controller.Delete(bens.ToString(), id.ToString(), CancellationToken.None);

        AssertProblem(missing, StatusCodes.Status404NotFound, "not-found");
        AssertSameProblem(missing, nonGuid);
        AssertSameProblem(missing, foreignTrip);
        _expenses.RowCount.Should().Be(1);
    }

    private ExpensesController Sut() =>
        new(new ExpenseService(_expenses, _people, _trips, _transaction, _time, _tripLock), new FakeCurrentUser(Anna));

    /// <summary>A request with the given fields. Participants are passed as a list, as the JSON body carries them.</summary>
    private static CreateExpenseRequest Request(string? description, string? amount, Guid? payer, params Guid[] participants) =>
        new(description, amount, payer, participants.ToList());

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

    /// <summary>Two refusals with the same cause must have the same status and body type (no per-request detail).</summary>
    private static void AssertSameProblem(IActionResult expected, IActionResult actual)
    {
        var a = (ObjectResult)expected;
        var b = (ObjectResult)actual;
        b.StatusCode.Should().Be(a.StatusCode);
        ((ProblemDetails)b.Value!).Type.Should().Be(((ProblemDetails)a.Value!).Type);
        ((ProblemDetails)b.Value!).Title.Should().Be(((ProblemDetails)a.Value!).Title);
    }

    /// <summary>Fake of the trip DAO. Every read is filtered by owner, as the real DAO must do (F4.R15).</summary>
    private sealed class FakeTripDao : ITripDao
    {
        private readonly List<Trip> _trips = new();

        public void Add(Trip trip) => _trips.Add(trip);

        /// <summary>Adds a trip for an owner and returns its id.</summary>
        public Guid AddTrip(Guid owner, string name)
        {
            var trip = new Trip(Guid.NewGuid(), owner, name, Start);
            _trips.Add(trip);
            return trip.Id;
        }

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
        public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
            await work(cancellationToken);
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }
}
