using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Core.People;

/// <summary>
/// People list, add and remove rules (F3.R1 to R3, R5 to R10). Pure rules over the DAO interfaces, the usage
/// sources and an injected clock. Every call is for one owner. The trip must belong to the owner before the
/// usage sources are asked, so their answers cannot reveal another user's trip (F3.R10).
/// </summary>
public sealed class PeopleService
{
    private readonly IPersonDao _people;
    private readonly ITripDao _trips;
    private readonly IReadOnlyList<IPersonUsageSource> _sources;
    private readonly TimeProvider _time;

    public PeopleService(IPersonDao people, ITripDao trips, IEnumerable<IPersonUsageSource> sources, TimeProvider time)
    {
        _people = people;
        _trips = trips;
        _sources = sources.ToList();
        _time = time;
    }

    /// <summary>The trip's people in the order added, with the in-use flags merged from every source (F3.R5, F3.R7).</summary>
    public async Task<PersonListResult> ListAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
    {
        if (await _trips.GetAsync(owner, tripId, cancellationToken) is null)
        {
            return PersonListResult.Refused(PersonFailure.NotFound);
        }

        var people = await _people.ListAsync(owner, tripId, cancellationToken);
        var inExpense = new HashSet<Guid>();
        var inPayment = new HashSet<Guid>();

        foreach (var source in _sources)
        {
            var ids = await source.GetPersonIdsInUseAsync(owner, tripId, cancellationToken);
            var target = source.Kind == PersonUsageKind.Expense ? inExpense : inPayment;
            target.UnionWith(ids);
        }

        var rows = people
            .Select(person => new PersonWithUsage(person, inExpense.Contains(person.Id), inPayment.Contains(person.Id)))
            .ToList();
        return PersonListResult.Found(rows);
    }

    /// <summary>Validates the name, then adds the person at the end of the list (F3.R1 to R3, R5). The DAO refuses a missing trip or a duplicate name.</summary>
    public async Task<PersonAddResult> AddAsync(Guid owner, Guid tripId, string? rawName, CancellationToken cancellationToken)
    {
        var check = PersonNameRules.Normalize(rawName);
        if (!check.IsValid)
        {
            return PersonAddResult.Refused(check.Failure!.Value);
        }

        var person = new Person(Guid.NewGuid(), tripId, check.Name, _time.GetUtcNow());
        return await _people.AddAsync(owner, tripId, person, cancellationToken) switch
        {
            PersonAddOutcome.Added => PersonAddResult.Added(person),
            PersonAddOutcome.Duplicate => PersonAddResult.Refused(PersonFailure.DuplicateName),
            _ => PersonAddResult.Refused(PersonFailure.NotFound),
        };
    }

    /// <summary>
    /// Removes the person unless an expense or payment uses them (F3.R6, R7). The sources are asked first, and the
    /// DAO's foreign key is the backstop for a race (F3.AC8).
    /// </summary>
    public async Task<PersonRemoveResult> RemoveAsync(Guid owner, Guid tripId, Guid personId, CancellationToken cancellationToken)
    {
        if (await _trips.GetAsync(owner, tripId, cancellationToken) is null)
        {
            return PersonRemoveResult.Refused(PersonFailure.NotFound);
        }

        foreach (var source in _sources)
        {
            var ids = await source.GetPersonIdsInUseAsync(owner, tripId, cancellationToken);
            if (ids.Contains(personId))
            {
                return PersonRemoveResult.Refused(PersonFailure.InUse);
            }
        }

        return await _people.DeleteAsync(owner, tripId, personId, cancellationToken) switch
        {
            PersonDeleteOutcome.Removed => PersonRemoveResult.Done(),
            PersonDeleteOutcome.InUse => PersonRemoveResult.Refused(PersonFailure.InUse),
            _ => PersonRemoveResult.Refused(PersonFailure.NotFound),
        };
    }
}
