using FluentAssertions;
using TripExpenseTracker.Core.SettleUp;

namespace TripExpenseTracker.Tests.SettleUp;

/// <summary>Suggestion rule, tie-breaking and determinism (F6.R1, F6.R2, F6.R3, F6.R4, F6.R11, F6.AC1, F6.AC2, F6.AC7, F6.AC14).</summary>
public class SuggestionRulesTests
{
    private static readonly Guid Sam = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Alex = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Jo = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Example1_JoPaysSam_ThenAlexPaysSam()
    {
        // Sam is owed 50.00, Alex owes 10.00, Jo owes 40.00 (F6.AC1).
        var result = SuggestionRules.Compute(new[]
        {
            (Sam, 1L, 5000L),
            (Alex, 2L, -1000L),
            (Jo, 3L, -4000L),
        });

        result.Should().Equal(
            new SuggestedPayment(Jo, Sam, 4000),
            new SuggestedPayment(Alex, Sam, 1000));
    }

    [Fact]
    public void Example3_AfterJoPays4000_OneSuggestionIsLeft_AlexPaysSam1000()
    {
        // Balances after Jo's 4000 payment is applied (F6.AC2, F6.R11).
        var result = SuggestionRules.Compute(new[]
        {
            (Sam, 1L, 1000L),
            (Alex, 2L, -1000L),
            (Jo, 3L, 0L),
        });

        result.Should().Equal(new SuggestedPayment(Alex, Sam, 1000));
    }

    [Fact]
    public void Tie_OnCreditors_PaysEarlierAddedFirst()
    {
        // Sam and Alex are each owed 10.00, Sam added first. Jo owes 20.00 (F6.AC7).
        var result = SuggestionRules.Compute(new[]
        {
            (Sam, 1L, 1000L),
            (Alex, 2L, 1000L),
            (Jo, 3L, -2000L),
        });

        result.Should().Equal(
            new SuggestedPayment(Jo, Sam, 1000),
            new SuggestedPayment(Jo, Alex, 1000));
    }

    [Fact]
    public void Tie_OnDebtors_PaysCreditorInSeqOrder()
    {
        // Alex and Jo each owe 10.00 (Alex added first), Sam is owed 20.00 (F6.R3).
        var result = SuggestionRules.Compute(new[]
        {
            (Sam, 1L, 2000L),
            (Alex, 2L, -1000L),
            (Jo, 3L, -1000L),
        });

        result.Should().Equal(
            new SuggestedPayment(Alex, Sam, 1000),
            new SuggestedPayment(Jo, Sam, 1000));
    }

    [Fact]
    public void ZeroBalances_GiveNoSuggestions()
    {
        var result = SuggestionRules.Compute(new[]
        {
            (Sam, 1L, 0L),
            (Alex, 2L, 0L),
        });

        result.Should().BeEmpty();
    }

    [Fact]
    public void EmptyInput_GivesNoSuggestions()
    {
        SuggestionRules.Compute(Array.Empty<(Guid PersonId, long Seq, long BalanceCents)>()).Should().BeEmpty();
    }

    [Fact]
    public void Null_Throws()
    {
        var act = () => SuggestionRules.Compute(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SameInput_GivesSameOutput_OnRepeatedRuns()
    {
        var input = new[] { (Sam, 1L, 700L), (Alex, 2L, -300L), (Jo, 3L, -400L) };

        var first = SuggestionRules.Compute(input);
        var second = SuggestionRules.Compute(input);

        second.Should().Equal(first);
    }

    [Fact]
    public void Property_ApplyingEverySuggestion_SettlesAllBalances_AndKeepsSumZero()
    {
        // Fixed seed, so the same balance sets are generated on every run (no clock, no shared state).
        var random = new Random(20261029);

        for (var run = 0; run < 300; run++)
        {
            var count = random.Next(2, 9);
            var balances = new long[count];
            long sum = 0;
            for (var i = 0; i < count - 1; i++)
            {
                balances[i] = random.Next(-50_000, 50_001);
                sum += balances[i];
            }

            balances[count - 1] = -sum;

            var people = Enumerable.Range(0, count)
                .Select(i => (PersonId: Guid.NewGuid(), Seq: (long)(i + 1), BalanceCents: balances[i]))
                .ToList();
            var nonZero = people.Count(p => p.BalanceCents != 0);

            var suggestions = SuggestionRules.Compute(people);

            var remaining = people.ToDictionary(p => p.PersonId, p => p.BalanceCents);
            foreach (var s in suggestions)
            {
                s.AmountCents.Should().BePositive();
                remaining[s.PayerPersonId] += s.AmountCents;
                remaining[s.PayeePersonId] -= s.AmountCents;
            }

            remaining.Values.Should().OnlyContain(v => v == 0);
            remaining.Values.Sum().Should().Be(0);
            suggestions.Count.Should().BeLessThanOrEqualTo(nonZero);
        }
    }

    [Fact]
    public void LargeAmounts_AreHandled_WithoutOverflow()
    {
        var result = SuggestionRules.Compute(new[]
        {
            (Sam, 1L, 4_000_000_000_000_000_000L),
            (Alex, 2L, -4_000_000_000_000_000_000L),
        });

        result.Should().Equal(new SuggestedPayment(Alex, Sam, 4_000_000_000_000_000_000L));
    }
}
