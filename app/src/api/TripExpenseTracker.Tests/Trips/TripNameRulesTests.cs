using FluentAssertions;
using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Tests.Trips;

/// <summary>Trip name rules (F2.R2, F2.AC2, F2.AC3).</summary>
public class TripNameRulesTests
{
    [Fact]
    public void Trims_the_ends_of_the_name()
    {
        var check = TripNameRules.Normalize("  Paris 2026  ");

        check.IsValid.Should().BeTrue();
        check.Name.Should().Be("Paris 2026");
    }

    [Fact]
    public void Only_spaces_is_refused_as_blank()
    {
        var check = TripNameRules.Normalize("     ");

        check.Failure.Should().Be(TripFailure.NameRequired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_or_empty_name_is_refused_as_blank(string? raw)
    {
        TripNameRules.Normalize(raw).Failure.Should().Be(TripFailure.NameRequired);
    }

    [Fact]
    public void A_name_of_exactly_100_characters_passes()
    {
        TripNameRules.Normalize(new string('a', 100)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_name_of_101_characters_is_too_long()
    {
        TripNameRules.Normalize(new string('a', 101)).Failure.Should().Be(TripFailure.NameTooLong);
    }

    [Fact]
    public void Length_is_counted_after_trimming()
    {
        var check = TripNameRules.Normalize("  " + new string('a', 100) + "  ");

        check.IsValid.Should().BeTrue();
        check.Name.Should().HaveLength(100);
    }

    [Fact]
    public void A_100_emoji_name_passes_and_101_fails_because_each_emoji_counts_as_one_character()
    {
        // Each emoji here is two UTF-16 units, so a string length check would refuse 100 of them.
        var hundred = string.Concat(Enumerable.Repeat("\U0001F600", 100));
        var hundredAndOne = string.Concat(Enumerable.Repeat("\U0001F600", 101));

        TripNameRules.Normalize(hundred).IsValid.Should().BeTrue();
        TripNameRules.Normalize(hundredAndOne).Failure.Should().Be(TripFailure.NameTooLong);
    }
}
