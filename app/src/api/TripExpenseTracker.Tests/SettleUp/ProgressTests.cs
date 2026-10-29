using FluentAssertions;
using TripExpenseTracker.Core.SettleUp;

namespace TripExpenseTracker.Tests.SettleUp;

/// <summary>Percentage and owed total (F6.R12, F6.AC8, F6.AC10, F6.AC14).</summary>
public class ProgressTests
{
    [Fact]
    public void NothingOwed_GivesNullPercentage_AndZeroOwed()
    {
        var progress = Progress.Compute(0, 0);

        progress.OwedCents.Should().Be(0);
        progress.Percentage.Should().BeNull();
        progress.PaidBackCents.Should().Be(0);
    }

    [Fact]
    public void PaidBack19900_OfOwed20000_Gives99_NotHundred()
    {
        // Paid back 199.00 of 200.00 owed (F6.AC10).
        var progress = Progress.Compute(19900, 100);

        progress.OwedCents.Should().Be(20000);
        progress.Percentage.Should().Be(99);
    }

    [Fact]
    public void Percentage_RoundsDown()
    {
        // 1 of 3 is 33.33...%, shown as 33.
        Progress.Compute(1, 2).Percentage.Should().Be(33);
    }

    [Fact]
    public void Percentage_IsHundred_OnlyWhenLeftIsZero()
    {
        var settled = Progress.Compute(20000, 0);

        settled.OwedCents.Should().Be(20000);
        settled.Percentage.Should().Be(100);
    }

    [Fact]
    public void NothingPaidYet_GivesZeroPercentage()
    {
        var progress = Progress.Compute(0, 5000);

        progress.OwedCents.Should().Be(5000);
        progress.Percentage.Should().Be(0);
    }

    [Fact]
    public void OwedEqualsPaidBackPlusLeft()
    {
        var progress = Progress.Compute(12345, 678);

        progress.OwedCents.Should().Be(12345 + 678);
    }

    [Fact]
    public void LargeAmounts_DoNotOverflow_InPercentage()
    {
        // paid * 100 would overflow long here; the calculation runs in Int128 (F6.R12).
        var progress = Progress.Compute(4_000_000_000_000_000_000L, 1_000_000_000_000_000_000L);

        progress.Percentage.Should().Be(80);
    }

    [Fact]
    public void NegativePaidBack_Throws()
    {
        var act = () => Progress.Compute(-1, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NegativeLeftToSettle_Throws()
    {
        var act = () => Progress.Compute(0, -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
