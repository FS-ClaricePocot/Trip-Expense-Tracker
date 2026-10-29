using FluentAssertions;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.Balances;
using TripExpenseTracker.Core.Payments;
using TripExpenseTracker.Core.SettleUp;
using TripExpenseTracker.Core.Trips;
using TripExpenseTracker.Tests.Support;

namespace TripExpenseTracker.Tests.Payments;

/// <summary>
/// Payment rules, mark as paid, delete and the settle-up read (F6.R5, R6, R8, R10, R11, R12, R14, R15, R18;
/// F6.AC3 to AC6, AC9, AC13, AC14), with fake DAOs, lock, transaction and clock.
/// Example: Jo owes Sam 40.00 and Alex owes Sam 10.00 (product.md, R6 Example 1 and 3).
/// </summary>
public class PaymentServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Anna = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ben = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TripId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid BensTripId = Guid.Parse("44444444-4444-4444-4444-444444444444");
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

    private readonly List<string> _calls = new();
    private readonly FakeTripDao _trips = new();
    private readonly FakeBalanceDao _balances;
    private readonly FakePaymentDao _payments;
    private readonly FakeTripLock _lock;
    private readonly FakeTransaction _transaction = new();
    private readonly FakeTimeProvider _time = new(Start);
    private readonly PaymentService _sut;
    private readonly SettleUpService _settle;

    public PaymentServiceTests()
    {
        _trips.Add(new Trip(TripId, Anna, "Paris 2026", Start));
        _trips.Add(new Trip(BensTripId, Ben, "Ben's trip", Start));

        _balances = new FakeBalanceDao(_calls);
        _balances.Set(TripId, Anna, new BalanceSnapshot(People, Expenses));

        _payments = new FakePaymentDao(_calls, new Dictionary<Guid, string> { [Jo] = "Jo", [Sam] = "Sam", [Alex] = "Alex" });
        _lock = new FakeTripLock(_calls, _trips);
        _sut = new PaymentService(_payments, _balances, _trips, _lock, _transaction, _time);
        _settle = new SettleUpService(_trips, _balances, _payments);
    }

    [Fact]
    public async Task Marking_Jo_pays_Sam_40_00_records_it_at_the_clock_time()
    {
        var result = await MarkAsync(Anna, TripId, Jo, Sam, 4000);

        result.Succeeded.Should().BeTrue();
        result.Payment!.PayerPersonId.Should().Be(Jo);
        result.Payment.PayeePersonId.Should().Be(Sam);
        result.Payment.AmountCents.Should().Be(4000);
        result.Payment.MarkedPaidAt.Should().Be(Start);
        _payments.Stored.Should().ContainSingle();
    }

    [Fact]
    public async Task A_triple_that_is_not_a_current_suggestion_is_refused_and_nothing_is_recorded()
    {
        var result = await MarkAsync(Anna, TripId, Jo, Sam, 1000);

        result.Failure.Should().Be(PaymentFailure.SuggestionsChanged);
        _payments.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Payer_equal_to_payee_is_refused()
    {
        var result = await MarkAsync(Anna, TripId, Sam, Sam, 4000);

        result.Failure.Should().Be(PaymentFailure.PayerPayeeSame);
        _payments.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task An_amount_of_zero_is_refused()
    {
        var result = await MarkAsync(Anna, TripId, Jo, Sam, 0);

        result.Failure.Should().Be(PaymentFailure.AmountNotPositive);
    }

    [Fact]
    public async Task A_payer_who_is_not_on_the_trip_is_refused()
    {
        var result = await MarkAsync(Anna, TripId, Guid.NewGuid(), Sam, 4000);

        result.Failure.Should().Be(PaymentFailure.PayerNotOnTrip);
    }

    /// <summary>A person removed after the check is refused by the database; the refusal names the missing side (F6.R6).</summary>
    [Theory]
    [InlineData(PaymentInsertOutcome.PayerNotOnTrip, PaymentFailure.PayerNotOnTrip)]
    [InlineData(PaymentInsertOutcome.PayeeNotOnTrip, PaymentFailure.PayeeNotOnTrip)]
    [InlineData(PaymentInsertOutcome.TripNotFound, PaymentFailure.NotFound)]
    public async Task A_refused_insert_maps_to_the_matching_failure_and_records_nothing(
        PaymentInsertOutcome refused, PaymentFailure expected)
    {
        _payments.RefuseNextInsert = refused;

        var result = await MarkAsync(Anna, TripId, Jo, Sam, 4000);

        result.Failure.Should().Be(expected);
        _payments.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_removes_the_payment_and_an_unknown_id_is_not_found()
    {
        var marked = await MarkAsync(Anna, TripId, Jo, Sam, 4000);

        var deleted = await _sut.DeleteAsync(Anna, TripId, marked.Payment!.Id, CancellationToken.None);
        var again = await _sut.DeleteAsync(Anna, TripId, marked.Payment.Id, CancellationToken.None);

        deleted.Succeeded.Should().BeTrue();
        _payments.Stored.Should().BeEmpty();
        again.Failure.Should().Be(PaymentFailure.PaymentNotFound);
    }

    /// <summary>Another user's trip is not found for mark, delete and read alike (F6.AC13).</summary>
    [Fact]
    public async Task A_foreign_or_missing_trip_is_not_found_for_every_call()
    {
        var mark = await MarkAsync(Ben, TripId, Jo, Sam, 4000);
        var delete = await _sut.DeleteAsync(Ben, TripId, Guid.NewGuid(), CancellationToken.None);
        var read = await _settle.GetAsync(Ben, TripId, CancellationToken.None);
        var missing = await MarkAsync(Anna, Guid.NewGuid(), Jo, Sam, 4000);

        mark.Failure.Should().Be(PaymentFailure.NotFound);
        delete.Failure.Should().Be(PaymentFailure.NotFound);
        read.Failure.Should().Be(PaymentFailure.NotFound);
        missing.Failure.Should().Be(PaymentFailure.NotFound);
        _calls.Should().NotContain("read");
        _payments.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Every_write_takes_the_trip_lock_before_reading_or_inserting()
    {
        var marked = await MarkAsync(Anna, TripId, Jo, Sam, 4000);
        _calls.Clear();

        await _sut.DeleteAsync(Anna, TripId, marked.Payment!.Id, CancellationToken.None);

        _calls.First().Should().Be("lock");

        _calls.Clear();
        await MarkAsync(Anna, TripId, Jo, Sam, 4000);

        _calls.First().Should().Be("lock");
        _calls.IndexOf("lock").Should().BeLessThan(_calls.IndexOf("read"));
    }

    [Fact]
    public async Task After_every_suggestion_is_paid_the_settle_up_view_is_settled()
    {
        await MarkAsync(Anna, TripId, Jo, Sam, 4000);
        await MarkAsync(Anna, TripId, Alex, Sam, 1000);

        var view = (await _settle.GetAsync(Anna, TripId, CancellationToken.None)).View!;

        view.Suggestions.Should().BeEmpty();
        view.LeftToSettleCents.Should().Be(0);
        view.Progress.PaidBackCents.Should().Be(5000);
        view.Progress.OwedCents.Should().Be(5000);
        view.Progress.Percentage.Should().Be(100);
        view.Payments.Should().HaveCount(2);
        view.Payments[0].PayerName.Should().Be("Alex", "the newest payment is listed first (F6.R7)");
    }

    [Fact]
    public async Task After_one_payment_the_view_shows_the_remaining_suggestion_and_progress()
    {
        await MarkAsync(Anna, TripId, Jo, Sam, 4000);

        var view = (await _settle.GetAsync(Anna, TripId, CancellationToken.None)).View!;

        view.Suggestions.Should().ContainSingle()
            .Which.Should().Be(new SettleUpSuggestion(Alex, "Alex", Sam, "Sam", 1000));
        view.LeftToSettleCents.Should().Be(1000);
        view.Progress.PaidBackCents.Should().Be(4000);
        view.Progress.OwedCents.Should().Be(5000);
        view.Progress.Percentage.Should().Be(80);
        view.TotalSpentCents.Should().Be(5000);
        view.ExpenseCount.Should().Be(2);
    }

    [Fact]
    public async Task Deleting_a_payment_restores_the_earlier_suggestions()
    {
        var marked = await MarkAsync(Anna, TripId, Jo, Sam, 4000);
        await _sut.DeleteAsync(Anna, TripId, marked.Payment!.Id, CancellationToken.None);

        var view = (await _settle.GetAsync(Anna, TripId, CancellationToken.None)).View!;

        view.Suggestions.Should().Equal(
            new SettleUpSuggestion(Jo, "Jo", Sam, "Sam", 4000),
            new SettleUpSuggestion(Alex, "Alex", Sam, "Sam", 1000));
        view.Payments.Should().BeEmpty();
    }

    private Task<PaymentRecordResult> MarkAsync(Guid owner, Guid tripId, Guid payer, Guid payee, long cents) =>
        _sut.MarkPaidAsync(owner, tripId, payer, payee, cents, CancellationToken.None);

    private sealed class FakeTransaction : IAuthTransaction
    {
        public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
            work(cancellationToken);
    }

    private sealed class FakeTripDao : ITripDao
    {
        private readonly List<Trip> _trips = new();

        public void Add(Trip trip) => _trips.Add(trip);

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

    private sealed class FakeBalanceDao(List<string> calls) : IBalanceDao
    {
        private readonly Dictionary<Guid, (Guid Owner, BalanceSnapshot Snapshot)> _trips = new();

        public void Set(Guid tripId, Guid owner, BalanceSnapshot snapshot) => _trips[tripId] = (owner, snapshot);

        public Task<BalanceSnapshot> GetSnapshotAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
        {
            calls.Add("read");
            if (_trips.TryGetValue(tripId, out var entry) && entry.Owner == owner)
            {
                return Task.FromResult(entry.Snapshot);
            }

            return Task.FromResult(new BalanceSnapshot(new List<BalancePerson>(), new List<BalanceExpense>()));
        }
    }

    private sealed class FakePaymentDao(List<string> calls, IReadOnlyDictionary<Guid, string> names) : IPaymentDao
    {
        private readonly List<(Guid Owner, StoredPayment Stored)> _rows = new();
        private long _seq;

        public IReadOnlyList<StoredPayment> Stored => _rows.Select(r => r.Stored).ToList();

        public Task<IReadOnlyList<StoredPayment>> ListAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
        {
            calls.Add("read");
            IReadOnlyList<StoredPayment> list = _rows
                .Where(r => r.Owner == owner && r.Stored.Payment.TripId == tripId)
                .Select(r => r.Stored)
                .ToList();
            return Task.FromResult(list);
        }

        /// <summary>Set to make the next insert refused by the database, as when a person is removed mid-request.</summary>
        public PaymentInsertOutcome? RefuseNextInsert { get; set; }

        public Task<PaymentInsertOutcome> InsertAsync(Guid owner, Payment payment, CancellationToken cancellationToken)
        {
            calls.Add("insert");
            if (RefuseNextInsert is { } refused)
            {
                RefuseNextInsert = null;
                return Task.FromResult(refused);
            }

            _rows.Add((owner, new StoredPayment(payment, ++_seq, names[payment.PayerPersonId], names[payment.PayeePersonId])));
            return Task.FromResult(PaymentInsertOutcome.Inserted);
        }

        public Task<bool> DeleteAsync(Guid owner, Guid tripId, Guid paymentId, CancellationToken cancellationToken)
        {
            calls.Add("delete");
            var removed = _rows.RemoveAll(r => r.Owner == owner && r.Stored.Payment.TripId == tripId && r.Stored.Payment.Id == paymentId);
            return Task.FromResult(removed > 0);
        }
    }

    private sealed class FakeTripLock(List<string> calls, FakeTripDao trips) : ITripLock
    {
        public Task<bool> LockAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
        {
            calls.Add("lock");
            return Task.FromResult(trips.Owns(owner, tripId));
        }
    }
}
