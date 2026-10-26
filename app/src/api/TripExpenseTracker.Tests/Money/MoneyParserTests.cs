using FluentAssertions;
using TripExpenseTracker.Core.Money;

namespace TripExpenseTracker.Tests.Money;

/// <summary>Amount text to whole cents (F4.R3, F4.AC3, F4.AC4), including the edge cases in the plan.</summary>
public class MoneyParserTests
{
    [Theory]
    [InlineData("10", 1000)]
    [InlineData("10.5", 1050)]
    [InlineData("10.50", 1050)]
    [InlineData("0.01", 1)]
    [InlineData("12.00", 1200)]
    [InlineData("  10.50  ", 1050)]
    public void Valid_amounts_become_whole_cents(string text, long expected)
    {
        var result = MoneyParser.ParseCents(text);

        result.Succeeded.Should().BeTrue();
        result.Cents.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_amount_is_amount_required(string? text)
    {
        MoneyParser.ParseCents(text).Failure.Should().Be(MoneyFailure.AmountRequired);
    }

    [Theory]
    [InlineData(".5")]
    [InlineData("10.")]
    [InlineData("+5")]
    [InlineData("1e3")]
    [InlineData("abc")]
    [InlineData("1,250")]
    [InlineData("1 000")]
    [InlineData("1 0.5")]
    [InlineData("10.5x")]
    public void Non_decimal_text_is_amount_invalid(string text)
    {
        MoneyParser.ParseCents(text).Failure.Should().Be(MoneyFailure.AmountInvalid);
    }

    [Theory]
    [InlineData("10.505")]
    [InlineData("0.001")]
    public void More_than_two_decimal_places_is_refused(string text)
    {
        MoneyParser.ParseCents(text).Failure.Should().Be(MoneyFailure.AmountTooManyDecimals);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.00")]
    [InlineData("-1")]
    [InlineData("-0.01")]
    public void Zero_or_negative_is_not_positive(string text)
    {
        MoneyParser.ParseCents(text).Failure.Should().Be(MoneyFailure.AmountNotPositive);
    }

    [Fact]
    public void Largest_long_of_cents_is_accepted()
    {
        var result = MoneyParser.ParseCents("92233720368547758.07");

        result.Succeeded.Should().BeTrue();
        result.Cents.Should().Be(long.MaxValue);
    }

    [Fact]
    public void One_cent_more_than_the_largest_long_is_amount_too_large()
    {
        MoneyParser.ParseCents("92233720368547758.08").Failure.Should().Be(MoneyFailure.AmountTooLarge);
    }

    [Fact]
    public void A_very_long_whole_number_is_amount_too_large()
    {
        MoneyParser.ParseCents("99999999999999999999").Failure.Should().Be(MoneyFailure.AmountTooLarge);
    }
}
