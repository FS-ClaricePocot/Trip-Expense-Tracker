using FluentAssertions;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.Trips;
using TripExpenseTracker.Tests.Support;

namespace TripExpenseTracker.Tests.Trips;

/// <summary>Trip create, get and delete rules (F2.R1 to R4, R9, F2.AC2 to AC5), with fake DAO, transaction and clock.</summary>
public class TripServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Anna = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ben = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly FakeTripDao _dao = new();
    private readonly FakeTransaction _transaction = new();
    private readonly FakeTimeProvider _time = new(Start);
    private readonly TripService _sut;

    public TripServiceTests()
    {
        _sut = new TripService(_dao, _transaction, _time);
    }

    [Fact]
    public async Task Create_saves_the_trimmed_name_with_the_owner_and_the_clock_time()
    {
        var result = await _sut.CreateAsync(Anna, "  Paris 2026  ", false, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var saved = _dao.Trips.Should().ContainSingle().Subject;
        saved.Name.Should().Be("Paris 2026");
        saved.OwnerUserId.Should().Be(Anna);
        saved.Id.Should().NotBe(Guid.Empty);
        saved.CreatedAt.Should().Be(Start);
        result.Trip.Should().Be(saved);
    }

    [Fact]
    public async Task Create_with_only_spaces_is_refused_and_inserts_nothing()
    {
        var result = await _sut.CreateAsync(Anna, "   ", false, CancellationToken.None);

        result.Failure.Should().Be(TripFailure.NameRequired);
        _dao.Trips.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_with_101_characters_is_refused_and_inserts_nothing()
    {
        var result = await _sut.CreateAsync(Anna, new string('a', 101), false, CancellationToken.None);

        result.Failure.Should().Be(TripFailure.NameTooLong);
        _dao.Trips.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_at_49_trips_succeeds()
    {
        _dao.Seed(Anna, 49);

        var result = await _sut.CreateAsync(Anna, "Trip 50", false, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _dao.CountFor(Anna).Should().Be(50);
    }

    [Fact]
    public async Task Create_at_50_trips_returns_limit_reached_and_inserts_nothing()
    {
        _dao.Seed(Anna, 50);

        var result = await _sut.CreateAsync(Anna, "Trip 51", false, CancellationToken.None);

        result.Failure.Should().Be(TripFailure.LimitReached);
        _dao.CountFor(Anna).Should().Be(50);
    }

    [Fact]
    public async Task Create_with_51_trips_is_also_refused()
    {
        _dao.Seed(Anna, 51);

        var result = await _sut.CreateAsync(Anna, "Trip 52", false, CancellationToken.None);

        result.Failure.Should().Be(TripFailure.LimitReached);
        _dao.CountFor(Anna).Should().Be(51);
    }

    [Fact]
    public async Task Create_with_a_duplicate_name_and_no_confirmation_returns_duplicate_and_inserts_nothing()
    {
        _dao.Seed(Anna, "Paris 2026");

        var result = await _sut.CreateAsync(Anna, "paris 2026", false, CancellationToken.None);

        result.Failure.Should().Be(TripFailure.DuplicateName);
        _dao.CountFor(Anna).Should().Be(1);
    }

    [Fact]
    public async Task Create_with_a_duplicate_name_and_confirmation_inserts()
    {
        _dao.Seed(Anna, "Paris 2026");

        var result = await _sut.CreateAsync(Anna, "paris 2026", true, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _dao.CountFor(Anna).Should().Be(2);
    }

    [Fact]
    public async Task The_duplicate_name_check_only_looks_at_the_owners_own_trips()
    {
        _dao.Seed(Ben, "Paris 2026");

        var result = await _sut.CreateAsync(Anna, "Paris 2026", false, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task The_limit_is_checked_before_the_duplicate()
    {
        _dao.Seed(Anna, 49);
        _dao.Seed(Anna, "Paris 2026");

        var result = await _sut.CreateAsync(Anna, "Paris 2026", false, CancellationToken.None);

        result.Failure.Should().Be(TripFailure.LimitReached);
    }

    [Fact]
    public async Task Create_locks_the_owner_before_counting_and_inserting_inside_one_transaction()
    {
        await _sut.CreateAsync(Anna, "Paris 2026", false, CancellationToken.None);

        _transaction.RunCount.Should().Be(1);
        _dao.Calls.Should().ContainInOrder("lock", "count", "exists", "insert");
    }

    [Fact]
    public async Task Get_returns_null_when_the_dao_has_no_trip_for_that_owner()
    {
        _dao.Seed(Ben, "Paris 2026");
        var benTrip = _dao.Trips.Single();

        var result = await _sut.GetAsync(Anna, benTrip.Id, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Delete_returns_false_when_nothing_was_removed()
    {
        _dao.Seed(Ben, "Paris 2026");
        var benTrip = _dao.Trips.Single();

        var removed = await _sut.DeleteAsync(Anna, benTrip.Id, CancellationToken.None);

        removed.Should().BeFalse();
        _dao.Trips.Should().ContainSingle();
    }

    private sealed class FakeTripDao : ITripDao
    {
        public List<Trip> Trips { get; } = new();
        public List<string> Calls { get; } = new();

        public void Seed(Guid owner, int count)
        {
            for (var i = 0; i < count; i++)
            {
                Trips.Add(new Trip(Guid.NewGuid(), owner, $"Trip {Trips.Count + 1}", Start));
            }
        }

        public void Seed(Guid owner, string name) => Trips.Add(new Trip(Guid.NewGuid(), owner, name, Start));

        public int CountFor(Guid owner) => Trips.Count(t => t.OwnerUserId == owner);

        public Task<IReadOnlyList<Trip>> ListAsync(Guid owner, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Trip>>(Trips.Where(t => t.OwnerUserId == owner).ToList());

        public Task<Trip?> GetAsync(Guid owner, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Trips.FirstOrDefault(t => t.OwnerUserId == owner && t.Id == id));

        public Task<int> CountAsync(Guid owner, CancellationToken cancellationToken)
        {
            Calls.Add("count");
            return Task.FromResult(CountFor(owner));
        }

        public Task<bool> ExistsWithNameAsync(Guid owner, string name, CancellationToken cancellationToken)
        {
            Calls.Add("exists");
            return Task.FromResult(Trips.Any(t =>
                t.OwnerUserId == owner && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)));
        }

        public Task InsertAsync(Trip trip, CancellationToken cancellationToken)
        {
            Calls.Add("insert");
            Trips.Add(trip);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Guid owner, Guid id, CancellationToken cancellationToken)
        {
            var removed = Trips.RemoveAll(t => t.OwnerUserId == owner && t.Id == id);
            return Task.FromResult(removed > 0);
        }

        public Task LockOwnerAsync(Guid owner, CancellationToken cancellationToken)
        {
            Calls.Add("lock");
            return Task.CompletedTask;
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
