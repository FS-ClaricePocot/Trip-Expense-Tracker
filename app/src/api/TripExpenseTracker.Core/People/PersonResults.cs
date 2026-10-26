namespace TripExpenseTracker.Core.People;

/// <summary>Why a people request was refused. The API maps each value to a status code and a code or type.</summary>
public enum PersonFailure
{
    /// <summary>Blank after trimming (F3.R2). Code <c>name_required</c>.</summary>
    NameRequired,

    /// <summary>More than 100 characters after trimming (F3.R2). Code <c>name_too_long</c>.</summary>
    NameTooLong,

    /// <summary>Same name as someone on the trip, ignoring capitals (F3.R3). Type <c>duplicate-person-name</c>.</summary>
    DuplicateName,

    /// <summary>The person is in an expense or payment, so cannot be removed (F3.R7). Type <c>person-in-use</c>.</summary>
    InUse,

    /// <summary>The trip is not the owner's or does not exist, or the person is not on that trip (F3.R10). Same response for all.</summary>
    NotFound,
}

/// <summary>What the DAO reports for an add. The owner check is in the DAO's query (F3.R10).</summary>
public enum PersonAddOutcome
{
    Added,

    /// <summary>The unique index on (trip, lower(name)) refused the name (F3.R3).</summary>
    Duplicate,

    /// <summary>No trip matches the owner and trip id.</summary>
    TripNotFound,
}

/// <summary>What the DAO reports for a removal (F3.R7, F3.R10).</summary>
public enum PersonDeleteOutcome
{
    Removed,

    /// <summary>The foreign key from an expense or payment refused the delete (F3.R7 backstop).</summary>
    InUse,

    /// <summary>No person with that id is on a trip the owner owns.</summary>
    NotFound,
}

/// <summary>An added person, or the reason it was refused. Never both.</summary>
public sealed record PersonAddResult(Person? Person, PersonFailure? Failure)
{
    public bool Succeeded => Person is not null;

    public static PersonAddResult Added(Person person) => new(person, null);

    public static PersonAddResult Refused(PersonFailure failure) => new(null, failure);
}

/// <summary>The trip's people in the order added, or <see cref="PersonFailure.NotFound"/>. Never both.</summary>
public sealed record PersonListResult(IReadOnlyList<PersonWithUsage>? People, PersonFailure? Failure)
{
    public bool Succeeded => People is not null;

    public static PersonListResult Found(IReadOnlyList<PersonWithUsage> people) => new(people, null);

    public static PersonListResult Refused(PersonFailure failure) => new(null, failure);
}

/// <summary>A removal that happened, or the reason it was refused (<see cref="PersonFailure.InUse"/> or <see cref="PersonFailure.NotFound"/>).</summary>
public sealed record PersonRemoveResult(PersonFailure? Failure)
{
    public bool Removed => Failure is null;

    public static PersonRemoveResult Done() => new(Failure: null);

    public static PersonRemoveResult Refused(PersonFailure failure) => new(Failure: failure);
}
