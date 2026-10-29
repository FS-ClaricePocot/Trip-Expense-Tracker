using FluentAssertions;
using TripExpenseTracker.Core.Balances;

namespace TripExpenseTracker.Tests.Balances;

/// <summary>The trip card status (F6.R19, F6.AC17 to AC20), and the trip list read that feeds it (F6.AC21).</summary>
public class TripStatusTests
{
    private static readonly Guid Sam = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Alex = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid Jo = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 2)]
    public void No_expenses_gives_none_with_no_count(int expenses, int suggestions)
    {
        TripStatus.From(expenses, suggestions).Should().Be(new TripStatus(SettleState.None, 0));
    }

    [Fact]
    public void One_or_more_suggestions_give_toSettle_with_that_count()
    {
        TripStatus.From(3, 2).Should().Be(new TripStatus(SettleState.ToSettle, 2));
        TripStatus.From(1, 1).Should().Be(new TripStatus(SettleState.ToSettle, 1));
    }

    [Fact]
    public void No_suggestions_with_expenses_gives_settled()
    {
        TripStatus.From(2, 0).Should().Be(new TripStatus(SettleState.Settled, 0));
    }

    [Fact]
    public async Task Example_1_gives_toSettle_with_two_payments()
    {
        var dao = new CountingBalanceDao();
        dao.Add(Owner, TripA, Example1Snapshot());

        var statuses = await NewService(dao).GetTripStatusesAsync(Owner, CancellationToken.None);

        statuses[TripA].Should().Be(new TripStatus(SettleState.ToSettle, 2));
    }

    [Fact]
    public async Task After_Jo_pays_Sam_the_card_shows_one_payment_left()
    {
        var dao = new CountingBalanceDao();
        var snapshot = Example1Snapshot() with
        {
            Payments = new List<BalancePayment> { new(Jo, Sam, 4000) },
        };
        dao.Add(Owner, TripA, snapshot);

        var statuses = await NewService(dao).GetTripStatusesAsync(Owner, CancellationToken.None);

        statuses[TripA].Should().Be(new TripStatus(SettleState.ToSettle, 1));
    }

    [Fact]
    public async Task All_balances_zero_with_expenses_gives_settled()
    {
        var dao = new CountingBalanceDao();
        var snapshot = Example1Snapshot() with
        {
            Payments = new List<BalancePayment> { new(Jo, Sam, 4000), new(Alex, Sam, 1000) },
        };
        dao.Add(Owner, TripA, snapshot);

        var statuses = await NewService(dao).GetTripStatusesAsync(Owner, CancellationToken.None);

        statuses[TripA].Should().Be(new TripStatus(SettleState.Settled, 0));
    }

    [Fact]
    public async Task The_list_read_runs_once_and_leaves_out_other_users_trips()
    {
        var dao = new CountingBalanceDao();
        dao.Add(Owner, TripA, Example1Snapshot());
        dao.Add(Owner, TripB, Example1Snapshot());
        dao.Add(OtherOwner, TripC, Example1Snapshot());

        var statuses = await NewService(dao).GetTripStatusesAsync(Owner, CancellationToken.None);

        dao.ListCalls.Should().Be(1);
        statuses.Keys.Should().BeEquivalentTo(new[] { TripA, TripB });
    }

    [Fact]
    public async Task A_trip_with_no_balance_data_has_no_entry()
    {
        var dao = new CountingBalanceDao();

        var statuses = await NewService(dao).GetTripStatusesAsync(Owner, CancellationToken.None);

        statuses.Should().BeEmpty();
    }

    private static readonly Guid Owner = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid OtherOwner = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid TripA = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid TripB = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid TripC = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    /// <summary>
    /// product.md Example 1 in the balances: Sam is owed 50.00, Alex owes 10.00, Jo owes 40.00. Jo's 40.00 is a share of an
    /// expense Sam paid, and Alex's 10.00 is a share of another. Sam is not a participant in either.
    /// </summary>
    private static BalanceSnapshot Example1Snapshot() => new(
        new List<BalancePerson> { new(Sam, "Sam", 1), new(Alex, "Alex", 2), new(Jo, "Jo", 3) },
        new List<BalanceExpense>
        {
            new(4000, Sam, new List<(Guid PersonId, long Seq)> { (Jo, 3) }),
            new(1000, Sam, new List<(Guid PersonId, long Seq)> { (Alex, 2) }),
        });

    private static BalanceService NewService(IBalanceDao dao) =>
        new(dao, trips: null!); // The owner-wide list read does not use the trip DAO.

    private sealed class CountingBalanceDao : IBalanceDao
    {
        private readonly Dictionary<Guid, (Guid Owner, BalanceSnapshot Snapshot)> _trips = new();

        public int ListCalls { get; private set; }

        public void Add(Guid owner, Guid tripId, BalanceSnapshot snapshot) => _trips[tripId] = (owner, snapshot);

        public Task<IReadOnlyDictionary<Guid, BalanceSnapshot>> ListForOwnerAsync(Guid owner, CancellationToken cancellationToken)
        {
            ListCalls++;
            IReadOnlyDictionary<Guid, BalanceSnapshot> snapshots = _trips
                .Where(entry => entry.Value.Owner == owner)
                .ToDictionary(entry => entry.Key, entry => entry.Value.Snapshot);
            return Task.FromResult(snapshots);
        }

        public Task<BalanceSnapshot> GetSnapshotAsync(Guid owner, Guid tripId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The list must not read one trip at a time.");
    }
}
