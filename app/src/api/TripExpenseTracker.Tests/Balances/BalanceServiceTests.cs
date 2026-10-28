using FluentAssertions;
using TripExpenseTracker.Core.Balances;
using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Tests.Balances;

/// <summary>Balance reads through the service: no caching (F5.R7), owner and trip checks (F5.R12, F5.AC12), and the paths of F5.AC14 (F5.R1, R4, R8).</summary>
public class BalanceServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Anna = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ben = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TripId = Guid.Parse("33333333-3333-3333-3333-333333333333");
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

    private BalanceService Sut() => new(_balances, _trips);

    public BalanceServiceTests()
    {
        _trips.Add(new Trip(TripId, Anna, "Paris 2026", Start));
        _balances.Snapshot = new BalanceSnapshot(People, []);
    }

    /// <summary>Adding an expense changes the balances, and deleting it restores the earlier ones (F5.AC7).</summary>
    [Fact]
    public async Task Adding_then_deleting_an_expense_returns_the_earlier_balances()
    {
        var sut = Sut();
        var before = await sut.GetBalancesAsync(Anna, TripId, CancellationToken.None);

        _balances.Snapshot = new BalanceSnapshot(People, [SamPaidThirtyForAlexAndJo()]);
        var during = await sut.GetBalancesAsync(Anna, TripId, CancellationToken.None);

        _balances.Snapshot = new BalanceSnapshot(People, []);
        var after = await sut.GetBalancesAsync(Anna, TripId, CancellationToken.None);

        during.Balances!.Select(b => b.BalanceCents).Should().Equal(3000L, -1500L, -1500L);
        after.Succeeded.Should().BeTrue();
        after.Balances!.Select(b => b.BalanceCents).Should().Equal(before.Balances!.Select(b => b.BalanceCents));
    }

    [Fact]
    public async Task Balances_list_every_person_in_seq_order_including_zero_balances()
    {
        var result = await Sut().GetBalancesAsync(Anna, TripId, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Balances!.Select(b => b.Name).Should().Equal("Sam", "Alex", "Jo");
        result.Balances!.Select(b => b.Status).Should().Equal(BalanceStatus.Settled, BalanceStatus.Settled, BalanceStatus.Settled);
    }

    [Fact]
    public async Task Example_expense_gives_owed_and_owes_balances()
    {
        _balances.Snapshot = new BalanceSnapshot(People, [SamPaidThirtyForAlexAndJo()]);

        var result = await Sut().GetBalancesAsync(Anna, TripId, CancellationToken.None);

        result.Balances!.Select(b => b.BalanceCents).Should().Equal(3000L, -1500L, -1500L);
        result.Balances!.Select(b => b.Status).Should().Equal(BalanceStatus.Owed, BalanceStatus.Owes, BalanceStatus.Owes);
    }

    [Fact]
    public async Task Summary_gives_total_spent_and_left_to_settle()
    {
        _balances.Snapshot = new BalanceSnapshot(People, [SamPaidThirtyForAlexAndJo()]);

        var result = await Sut().GetSummaryAsync(Anna, TripId, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Summary.Should().Be(new BalanceSummary(TotalSpentCents: 3000, LeftToSettleCents: 3000));
    }

    [Fact]
    public async Task A_foreign_trip_is_not_found_for_both_calls()
    {
        _balances.Snapshot = new BalanceSnapshot(People, [SamPaidThirtyForAlexAndJo()]);
        var sut = Sut();

        var balances = await sut.GetBalancesAsync(Ben, TripId, CancellationToken.None);
        var summary = await sut.GetSummaryAsync(Ben, TripId, CancellationToken.None);

        balances.Succeeded.Should().BeFalse();
        balances.Failure.Should().Be(BalanceServiceFailure.NotFound);
        summary.Succeeded.Should().BeFalse();
        summary.Failure.Should().Be(BalanceServiceFailure.NotFound);
    }

    [Fact]
    public async Task A_missing_trip_is_not_found_for_both_calls()
    {
        var sut = Sut();
        var missing = Guid.Parse("77777777-7777-7777-7777-777777777777");

        var balances = await sut.GetBalancesAsync(Anna, missing, CancellationToken.None);
        var summary = await sut.GetSummaryAsync(Anna, missing, CancellationToken.None);

        balances.Failure.Should().Be(BalanceServiceFailure.NotFound);
        summary.Failure.Should().Be(BalanceServiceFailure.NotFound);
    }

    [Fact]
    public async Task A_failed_zero_sum_check_returns_balances_inconsistent_and_no_balances()
    {
        _balances.Snapshot = new BalanceSnapshot(People, [SamPaidThirtyForAlexAndJo()]);
        var sut = new BalanceService(_balances, _trips, _ => BalanceFailure.BalancesInconsistent);

        var result = await sut.GetBalancesAsync(Anna, TripId, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Balances.Should().BeNull();
        result.Failure.Should().Be(BalanceServiceFailure.BalancesInconsistent);
    }

    private static BalanceExpense SamPaidThirtyForAlexAndJo() =>
        new(3000, Sam, [(Alex, 2), (Jo, 3)]);

    /// <summary>Like the real DAO: empty lists for a trip the owner does not have, and the snapshot only for the owner's trip.</summary>
    private sealed class FakeBalanceDao : IBalanceDao
    {
        public BalanceSnapshot Snapshot { get; set; } = new([], []);

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
}
