using TripExpenseTracker.Core.Auth;

namespace TripExpenseTracker.Core.Trips;

/// <summary>
/// Trip create, list, get and delete rules (F2.R1 to R5, R9). Pure rules over the DAO interface and injected
/// time. The unit of work comes from <see cref="IAuthTransaction"/>, which runs any work as one transaction.
/// </summary>
public sealed class TripService
{
    private readonly ITripDao _trips;
    private readonly IAuthTransaction _transaction;
    private readonly TimeProvider _time;

    public TripService(ITripDao trips, IAuthTransaction transaction, TimeProvider time)
    {
        _trips = trips;
        _transaction = transaction;
        _time = time;
    }

    public Task<IReadOnlyList<Trip>> ListAsync(Guid owner, CancellationToken cancellationToken) =>
        _trips.ListAsync(owner, cancellationToken);

    /// <summary>
    /// Validates the name, then in one transaction: lock the owner, refuse at 50 trips, refuse a same-name trip
    /// unless confirmed, insert (F2.R1 to R4). The limit is checked before the duplicate.
    /// </summary>
    public async Task<TripCreateResult> CreateAsync(Guid owner, string? rawName, bool confirmDuplicate, CancellationToken cancellationToken)
    {
        var check = TripNameRules.Normalize(rawName);
        if (!check.IsValid)
        {
            return TripCreateResult.Refused(check.Failure!.Value);
        }

        var name = check.Name;
        return await _transaction.RunAsync(async token =>
        {
            await _trips.LockOwnerAsync(owner, token);

            if (await _trips.CountAsync(owner, token) >= TripLimits.MaxTripsPerUser)
            {
                return TripCreateResult.Refused(TripFailure.LimitReached);
            }

            if (!confirmDuplicate && await _trips.ExistsWithNameAsync(owner, name, token))
            {
                return TripCreateResult.Refused(TripFailure.DuplicateName);
            }

            var trip = new Trip(Guid.NewGuid(), owner, name, _time.GetUtcNow());
            await _trips.InsertAsync(trip, token);
            return TripCreateResult.Created(trip);
        }, cancellationToken);
    }

    /// <summary>Returns the owner's trip, or null when it is missing or belongs to someone else (F2.R9).</summary>
    public Task<Trip?> GetAsync(Guid owner, Guid id, CancellationToken cancellationToken) =>
        _trips.GetAsync(owner, id, cancellationToken);

    /// <summary>Deletes the owner's trip. Returns false when there is nothing to delete, which the API reports as not found (F2.R9).</summary>
    public Task<bool> DeleteAsync(Guid owner, Guid id, CancellationToken cancellationToken) =>
        _trips.DeleteAsync(owner, id, cancellationToken);
}
