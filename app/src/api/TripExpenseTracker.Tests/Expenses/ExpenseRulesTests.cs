using FluentAssertions;
using TripExpenseTracker.Core.Expenses;

namespace TripExpenseTracker.Tests.Expenses;

/// <summary>Description trimming and length, payer and participant rules (F4.R2, F4.R5, F4.R6, F4.AC5 to AC7).</summary>
public class ExpenseRulesTests
{
    private static readonly Guid Payer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Alex = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Jo = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly IReadOnlyList<Guid> Two = new[] { Payer, Alex };

    [Fact]
    public void Description_is_trimmed_before_it_is_saved()
    {
        var check = ExpenseRules.Check("  Dinner  ", Payer, Two);

        check.IsValid.Should().BeTrue();
        check.Description.Should().Be("Dinner");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Blank_description_is_refused(string? text)
    {
        ExpenseRules.Check(text, Payer, Two).Failure.Should().Be(ExpenseRuleFailure.DescriptionRequired);
    }

    [Fact]
    public void Hundred_characters_pass_and_a_hundred_and_one_fail()
    {
        var hundred = new string('a', 100);
        var hundredAndOne = new string('a', 101);

        ExpenseRules.Check(hundred, Payer, Two).IsValid.Should().BeTrue();
        ExpenseRules.Check(hundredAndOne, Payer, Two).Failure.Should().Be(ExpenseRuleFailure.DescriptionTooLong);
    }

    [Fact]
    public void Hundred_emoji_pass_and_a_hundred_and_one_fail()
    {
        var hundred = string.Concat(Enumerable.Repeat("\U0001F600", 100));
        var hundredAndOne = string.Concat(Enumerable.Repeat("\U0001F600", 101));

        ExpenseRules.Check(hundred, Payer, Two).IsValid.Should().BeTrue();
        ExpenseRules.Check(hundredAndOne, Payer, Two).Failure.Should().Be(ExpenseRuleFailure.DescriptionTooLong);
    }

    [Fact]
    public void Missing_payer_is_refused()
    {
        ExpenseRules.Check("Dinner", null, Two).Failure.Should().Be(ExpenseRuleFailure.PayerRequired);
    }

    [Fact]
    public void No_participants_is_refused()
    {
        ExpenseRules.Check("Dinner", Payer, Array.Empty<Guid>()).Failure.Should().Be(ExpenseRuleFailure.ParticipantsRequired);
    }

    [Fact]
    public void Missing_participant_list_is_refused()
    {
        ExpenseRules.Check("Dinner", Payer, null).Failure.Should().Be(ExpenseRuleFailure.ParticipantsRequired);
    }

    [Fact]
    public void Duplicate_participant_is_refused()
    {
        var check = ExpenseRules.Check("Dinner", Payer, new[] { Alex, Jo, Alex });

        check.Failure.Should().Be(ExpenseRuleFailure.ParticipantDuplicate);
    }

    [Fact]
    public void Payer_who_is_not_a_participant_is_accepted()
    {
        // F4.R6: the payer does not have to be one of the participants.
        var check = ExpenseRules.Check("Dinner", Payer, new[] { Alex, Jo });

        check.IsValid.Should().BeTrue();
    }
}
