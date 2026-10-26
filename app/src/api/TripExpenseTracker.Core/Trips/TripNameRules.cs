namespace TripExpenseTracker.Core.Trips;

/// <summary>Result of checking a trip name. <see cref="Name"/> is the trimmed text that would be saved.</summary>
public sealed record TripNameCheck(string Name, TripFailure? Failure)
{
    public bool IsValid => Failure is null;
}

/// <summary>
/// Trip name rules (F2.R2): trim the ends, refuse blank, refuse more than 100 characters. A character is a
/// Unicode code point, so one emoji counts as one. Any other characters are allowed. Pure logic, no I/O.
/// </summary>
public static class TripNameRules
{
    public static TripNameCheck Normalize(string? raw)
    {
        var name = (raw ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return new TripNameCheck(name, TripFailure.NameRequired);
        }

        return CountCodePoints(name) > TripLimits.MaxNameLength
            ? new TripNameCheck(name, TripFailure.NameTooLong)
            : new TripNameCheck(name, null);
    }

    private static int CountCodePoints(string text)
    {
        var count = 0;
        var runes = text.EnumerateRunes();
        while (runes.MoveNext())
        {
            count++;
        }

        return count;
    }
}
