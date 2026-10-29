using FluentAssertions;
using TripExpenseTracker.Core.Balances;

namespace TripExpenseTracker.Tests.Balances;

/// <summary>Zero-sum check and summary tiles (F5.R3, F5.R8, F5.AC8, F5.AC9, F5.AC10, F5.AC16).</summary>
public class BalanceCheckAndSummaryTests
{
    private static readonly Guid Sam = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Alex = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Jo = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly IReadOnlyList<(Guid PersonId, long Seq)> Everyone = new[] { (Sam, 1L), (Alex, 2L), (Jo, 3L) };

    private static readonly IReadOnlyList<BalanceExpense> Example1Expenses = new[]
    {
        new BalanceExpense(9000, Sam, Everyone),
        new BalanceExpense(3000, Alex, Everyone),
    };

    private static IReadOnlyList<PersonBalance> Example1Balances() => new[]
    {
        new PersonBalance(Sam, "Sam", 5000),
        new PersonBalance(Alex, "Alex", -1000),
        new PersonBalance(Jo, "Jo", -4000),
    };

    [Fact]
    public void Check_passes_when_balances_add_to_zero()
    {
        BalanceCheck.Verify(Example1Balances()).Should().BeNull();
    }

    [Fact]
    public void Check_refuses_a_non_zero_total_with_balances_inconsistent()
    {
        // F5.AC16: forced inconsistency.
        var broken = new[]
        {
            new PersonBalance(Sam, "Sam", 5000),
            new PersonBalance(Alex, "Alex", -1000),
        };

        BalanceCheck.Verify(broken).Should().Be(BalanceFailure.BalancesInconsistent);
    }

    [Fact]
    public void Check_passes_for_an_empty_list()
    {
        BalanceCheck.Verify(Array.Empty<PersonBalance>()).Should().BeNull();
    }

    [Fact]
    public void Example_1_summary_has_total_spent_120_00_and_left_to_settle_50_00()
    {
        // F5.AC9 (left to settle = sum of positive balances), F5.AC10 (total spent).
        var summary = BalanceSummary.Compute(Example1Expenses, Example1Balances());

        summary.TotalSpentCents.Should().Be(12000);
        summary.LeftToSettleCents.Should().Be(5000);
    }

    [Fact]
    public void Left_to_settle_is_zero_when_every_balance_is_zero()
    {
        // F5.AC8: a trip with people and no expenses.
        var zeros = new[]
        {
            new PersonBalance(Sam, "Sam", 0),
            new PersonBalance(Alex, "Alex", 0),
        };

        var summary = BalanceSummary.Compute(Array.Empty<BalanceExpense>(), zeros);

        summary.TotalSpentCents.Should().Be(0);
        summary.LeftToSettleCents.Should().Be(0);
    }
}
