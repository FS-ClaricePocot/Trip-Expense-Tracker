namespace TripExpenseTracker.Core.Auth;

/// <summary>Email rules from F1.R2 and F1.R13. Pure logic, no I/O.</summary>
public static class EmailAddress
{
    /// <summary>Trims surrounding spaces, then lower-cases with the invariant culture.</summary>
    public static string Normalize(string? raw) => (raw ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// True when the normalized value has exactly one '@' and at least one '.' after it.
    /// This is the whole check (F1.R13). The app does not verify that the address exists.
    /// </summary>
    public static bool IsWellFormed(string normalized)
    {
        var at = normalized.IndexOf('@');
        if (at < 0 || normalized.IndexOf('@', at + 1) >= 0)
        {
            return false;
        }

        return normalized.IndexOf('.', at + 1) >= 0;
    }
}
