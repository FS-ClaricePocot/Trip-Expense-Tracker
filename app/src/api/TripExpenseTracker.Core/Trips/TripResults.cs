namespace TripExpenseTracker.Core.Trips;

/// <summary>Why a trip request was refused. The API maps each value to a status code and a code or type.</summary>
public enum TripFailure
{
    /// <summary>Blank after trimming (F2.R2). Code <c>name_required</c>.</summary>
    NameRequired,

    /// <summary>More than 100 characters after trimming (F2.R2). Code <c>name_too_long</c>.</summary>
    NameTooLong,

    /// <summary>Same name as one of the owner's trips, and not confirmed (F2.R3). Type <c>duplicate-trip-name</c>.</summary>
    DuplicateName,

    /// <summary>The owner already has 50 trips (F2.R4). Type <c>trip-limit-reached</c>.</summary>
    LimitReached,
}

/// <summary>A created trip, or the reason it was refused. Never both.</summary>
public sealed record TripCreateResult(Trip? Trip, TripFailure? Failure)
{
    public bool Succeeded => Trip is not null;

    public static TripCreateResult Created(Trip trip) => new(trip, null);

    public static TripCreateResult Refused(TripFailure failure) => new(null, failure);
}
