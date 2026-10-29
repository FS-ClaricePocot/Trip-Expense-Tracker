using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.Balances;
using TripExpenseTracker.Core.Payments;
using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Tests.Support;

/// <summary>Fake trip DAO for the payment and settle-up controller tests. Every read is filtered by owner (F6.R15).</summary>
public sealed class FakeSettleTrips : ITripDao
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

/// <summary>Fake balance DAO: one snapshot per trip, returned only to its owner.</summary>
public sealed class FakeSettleBalances : IBalanceDao
{
    private readonly Dictionary<Guid, (Guid Owner, BalanceSnapshot Snapshot)> _trips = new();

    public void Set(Guid tripId, Guid owner, BalanceSnapshot snapshot) => _trips[tripId] = (owner, snapshot);

    public Task<BalanceSnapshot> GetSnapshotAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
    {
        if (_trips.TryGetValue(tripId, out var entry) && entry.Owner == owner)
        {
            return Task.FromResult(entry.Snapshot);
        }

        return Task.FromResult(new BalanceSnapshot(new List<BalancePerson>(), new List<BalanceExpense>()));
    }

    /// <summary>The snapshots of the trips this owner has, as the owner-wide read returns them.</summary>
    public Task<IReadOnlyDictionary<Guid, BalanceSnapshot>> ListForOwnerAsync(Guid owner, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, BalanceSnapshot> snapshots = _trips
            .Where(entry => entry.Value.Owner == owner)
            .ToDictionary(entry => entry.Key, entry => entry.Value.Snapshot);
        return Task.FromResult(snapshots);
    }
}

/// <summary>Fake payment DAO. Rows are kept with an insertion seq, and filtered by owner and trip as the real DAO must do.</summary>
public sealed class FakeSettlePayments(IReadOnlyDictionary<Guid, string> names) : IPaymentDao
{
    private readonly List<(Guid Owner, StoredPayment Stored)> _rows = new();
    private long _seq;

    public int RowCount => _rows.Count;

    public Task<IReadOnlyList<StoredPayment>> ListAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
    {
        IReadOnlyList<StoredPayment> list = _rows
            .Where(r => r.Owner == owner && r.Stored.Payment.TripId == tripId)
            .Select(r => r.Stored)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<bool> InsertAsync(Guid owner, Payment payment, CancellationToken cancellationToken)
    {
        _rows.Add((owner, new StoredPayment(payment, ++_seq, names[payment.PayerPersonId], names[payment.PayeePersonId])));
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid owner, Guid tripId, Guid paymentId, CancellationToken cancellationToken)
    {
        var removed = _rows.RemoveAll(r => r.Owner == owner && r.Stored.Payment.TripId == tripId && r.Stored.Payment.Id == paymentId);
        return Task.FromResult(removed > 0);
    }
}

/// <summary>Runs the work directly, with no database.</summary>
public sealed class FakeSettleTransaction : IAuthTransaction
{
    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
        work(cancellationToken);
}

/// <summary>A fake <see cref="ICurrentUser"/> with a fixed id.</summary>
public sealed class FakeSettleCurrentUser(Guid userId) : ICurrentUser
{
    public Guid UserId { get; } = userId;
}
