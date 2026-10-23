namespace TripExpenseTracker.Core.People;

/// <summary>Result of checking a person name. <see cref="Name"/> is the trimmed text that would be saved.</summary>
public sealed record PersonNameCheck(string Name, PersonFailure? Failure)
{
    public bool IsValid => Failure is null;
}

/// <summary>
/// Person name rules (F3.R2): trim the ends, refuse blank, refuse more than 100 characters. A character is a
/// Unicode code point, so one emoji counts as one. Any other characters are allowed. Pure logic, no I/O.
/// Same behavior as the trip name rule (F2.R2), kept as its own class.
/// </summary>
public static class PersonNameRules
{
    public const int MaxNameLength = 100;

    public static PersonNameCheck Normalize(string? raw)
    {
        var name = (raw ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return new PersonNameCheck(name, PersonFailure.NameRequired);
        }

        return CountCodePoints(name) > MaxNameLength
            ? new PersonNameCheck(name, PersonFailure.NameTooLong)
            : new PersonNameCheck(name, null);
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
