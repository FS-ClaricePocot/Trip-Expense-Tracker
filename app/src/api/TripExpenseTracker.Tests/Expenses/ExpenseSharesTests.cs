using FluentAssertions;
using TripExpenseTracker.Core.Expenses;

namespace TripExpenseTracker.Tests.Expenses;

/// <summary>Equal split to the cent with leftover cents to the earliest-added (F4.R7, F4.AC1, F4.AC2).</summary>
public class ExpenseSharesTests
{
    private static readonly Guid Sam = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Alex = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Jo = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Ninety_split_three_ways_gives_thirty_each()
    {
        var shares = ExpenseShares.Compute(9000, new[] { (Sam, 1L), (Alex, 2L), (Jo, 3L) });

        shares.Select(s => s.Cents).Should().Equal(3000, 3000, 3000);
    }

    [Fact]
    public void Ten_split_three_ways_gives_leftover_cent_to_the_earliest_added()
    {
        // Worked example 2 (F4.AC2): Sam, Alex, Jo in that order.
        var shares = ExpenseShares.Compute(1000, new[] { (Sam, 1L), (Alex, 2L), (Jo, 3L) });

        shares.Should().Equal(
            new ExpenseShare(Sam, 334),
            new ExpenseShare(Alex, 333),
            new ExpenseShare(Jo, 333));
    }

    [Fact]
    public void Order_follows_seq_not_the_order_given()
    {
        var shares = ExpenseShares.Compute(1000, new[] { (Jo, 3L), (Sam, 1L), (Alex, 2L) });

        shares.Should().Equal(
            new ExpenseShare(Sam, 334),
            new ExpenseShare(Alex, 333),
            new ExpenseShare(Jo, 333));
    }

    [Fact]
    public void One_cent_split_three_ways_gives_the_cent_to_the_earliest_only()
    {
        var shares = ExpenseShares.Compute(1, new[] { (Sam, 1L), (Alex, 2L), (Jo, 3L) });

        shares.Select(s => s.Cents).Should().Equal(1, 0, 0);
    }

    [Fact]
    public void A_single_participant_gets_the_whole_amount()
    {
        var shares = ExpenseShares.Compute(4250, new[] { (Sam, 1L) });

        shares.Should().Equal(new ExpenseShare(Sam, 4250));
    }

    [Fact]
    public void Empty_participant_list_is_refused()
    {
        var act = () => ExpenseShares.Compute(1000, Array.Empty<(Guid, long)>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Shares_always_add_up_to_the_amount_over_many_random_cases()
    {
        var random = new Random(20261026);
        for (var trial = 0; trial < 2000; trial++)
        {
            var amount = random.NextInt64(1, 1_000_000_000_000L);
            var count = random.Next(1, 30);
            var people = Enumerable.Range(0, count)
                .Select(i => (Guid.NewGuid(), (long)(i + 1)))
                .ToList();

            var shares = ExpenseShares.Compute(amount, people);

            shares.Should().HaveCount(count);
            shares.Sum(s => s.Cents).Should().Be(amount);
            var spread = shares.Max(s => s.Cents) - shares.Min(s => s.Cents);
            spread.Should().BeLessThanOrEqualTo(1);
        }
    }
}
