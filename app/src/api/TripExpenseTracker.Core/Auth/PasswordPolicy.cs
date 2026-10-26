namespace TripExpenseTracker.Core.Auth;

public enum PasswordLengthResult
{
    Ok,
    TooShort,
    TooLong,
}

/// <summary>
/// Password length rule from F1.R3 and F1.R14: 8 to 128 characters.
/// A character is a Unicode code point, so one emoji counts as one.
/// Passwords are never trimmed and any characters are allowed. Pure logic, no I/O.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static int CountCharacters(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return 0;
        }

        var count = 0;
        var runes = password.EnumerateRunes();
        while (runes.MoveNext())
        {
            count++;
        }

        return count;
    }

    public static PasswordLengthResult CheckLength(string? password)
    {
        var count = CountCharacters(password);
        if (count < MinLength)
        {
            return PasswordLengthResult.TooShort;
        }

        return count > MaxLength ? PasswordLengthResult.TooLong : PasswordLengthResult.Ok;
    }
}
