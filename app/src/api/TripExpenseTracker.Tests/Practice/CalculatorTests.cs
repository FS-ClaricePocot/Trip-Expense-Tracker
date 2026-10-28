using FluentAssertions;
using TripExpenseTracker.Core.Practice;

namespace TripExpenseTracker.Tests.Practice;

/// <summary>Practice calculator (F0.AC2).</summary>
public class CalculatorTests
{
    [Fact]
    public void F0_AC2_adds_two_positive_numbers()
    {
        Calculator.Add(2, 3).Should().Be(5);
    }

    [Fact]
    public void F0_AC2_adds_a_negative_and_a_positive_to_zero()
    {
        Calculator.Add(-1, 1).Should().Be(0);
    }
}
