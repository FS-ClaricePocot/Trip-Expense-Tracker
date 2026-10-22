namespace TripExpenseTracker.Core.Auth;

/// <summary>Auth settings. Bound from the "Auth" configuration section in the API project.</summary>
public sealed class AuthOptions
{
    /// <summary>A session older than this is rejected and deleted (F1.R6). Default 24 hours (Decisions log).</summary>
    public TimeSpan SessionMaxAge { get; set; } = TimeSpan.FromHours(24);

    /// <summary>How often the background job deletes sessions older than <see cref="SessionMaxAge"/> (F1.R6). Default 1 hour.</summary>
    public TimeSpan SessionCleanupInterval { get; set; } = TimeSpan.FromHours(1);
}
