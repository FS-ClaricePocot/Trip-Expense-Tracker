using System.Globalization;
using System.Text.RegularExpressions;

namespace TripExpenseTracker.Core.Money;

/// <summary>Why an amount was refused (F4.R3). The API maps each value to a code.</summary>
public enum MoneyFailure
{
    /// <summary>No amount was entered. Code <c>amount_required</c>.</summary>
    AmountRequired,

    /// <summary>Not a decimal such as <c>12.50</c>. Code <c>amount_invalid</c>.</summary>
    AmountInvalid,

    /// <summary>More than 2 decimal places. Code <c>amount_too_many_decimals</c>.</summary>
    AmountTooManyDecimals,

    /// <summary>Zero or negative. Code <c>amount_not_positive</c>.</summary>
    AmountNotPositive,

    /// <summary>Does not fit in a stored <c>long</c> of cents. Code <c>amount_too_large</c>.</summary>
    AmountTooLarge,
}

/// <summary>An amount in whole cents, or the reason it was refused. Never both.</summary>
public sealed record MoneyParseResult(long? Cents, MoneyFailure? Failure)
{
    public bool Succeeded => Cents is not null;

    public static MoneyParseResult Parsed(long cents) => new(cents, null);

    public static MoneyParseResult Refused(MoneyFailure failure) => new(null, failure);
}

/// <summary>
/// The only place that turns amount text into cents (F4.R3, architecture rule "money as integers").
/// Accepts digits, optionally followed by a single '.' and 1 or 2 digits, after trimming outer spaces.
/// A leading '-' before a valid amount is reported as not positive. Everything else is invalid.
/// Pure logic, no I/O.
/// </summary>
public static class MoneyParser
{
    private static readonly Regex DecimalShape = new(@"^(?<whole>[0-9]+)(?:\.(?<frac>[0-9]+))?\z", RegexOptions.CultureInvariant);

    public static MoneyParseResult ParseCents(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return MoneyParseResult.Refused(MoneyFailure.AmountRequired);
        }

        var negative = trimmed[0] == '-';
        var body = negative ? trimmed[1..] : trimmed;

        var match = DecimalShape.Match(body);
        if (!match.Success)
        {
            return MoneyParseResult.Refused(MoneyFailure.AmountInvalid);
        }

        var whole = match.Groups["whole"].Value;
        var frac = match.Groups["frac"].Success ? match.Groups["frac"].Value : string.Empty;
        if (frac.Length > 2)
        {
            return MoneyParseResult.Refused(MoneyFailure.AmountTooManyDecimals);
        }

        if (negative)
        {
            return MoneyParseResult.Refused(MoneyFailure.AmountNotPositive);
        }

        var digits = whole + frac.PadRight(2, '0');
        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var cents))
        {
            return MoneyParseResult.Refused(MoneyFailure.AmountTooLarge);
        }

        return cents == 0
            ? MoneyParseResult.Refused(MoneyFailure.AmountNotPositive)
            : MoneyParseResult.Parsed(cents);
    }
}
