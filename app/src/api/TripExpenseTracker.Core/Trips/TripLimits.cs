namespace TripExpenseTracker.Core.Trips;

/// <summary>Fixed limits from the F2 spec: at most 50 trips per user (F2.R4), names up to 100 characters (F2.R2).</summary>
public static class TripLimits
{
    public const int MaxTripsPerUser = 50;
    public const int MaxNameLength = 100;
}
