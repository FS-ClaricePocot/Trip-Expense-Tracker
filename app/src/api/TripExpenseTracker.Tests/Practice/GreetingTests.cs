using FluentAssertions;
using TripExpenseTracker.Core.Practice;

namespace TripExpenseTracker.Tests.Practice;

/// <summary>Practice greeting (F0.AC1).</summary>
public class GreetingTests
{
    [Fact]
    public void F0_AC1_greets_the_given_name()
    {
        Greeting.For("Sam").Should().Be("Hello, Sam!");
    }
}
