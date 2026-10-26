using FluentAssertions;
using TripExpenseTracker.Api.Auth;
using TripExpenseTracker.Core.Auth;
using Xunit;

namespace TripExpenseTracker.Tests.Auth;

/// <summary>Startup rules for the Auth settings: both durations must be greater than zero.</summary>
public class AuthOptionsValidationTests
{
    private static readonly AuthOptionsValidation Sut = new();

    [Fact]
    public void DefaultAndExplicitPositiveValues_Pass()
    {
        Sut.Validate(null, new AuthOptions()).Succeeded.Should().BeTrue();
        Sut.Validate(null, new AuthOptions
        {
            SessionMaxAge = TimeSpan.FromDays(1),
            SessionCleanupInterval = TimeSpan.FromHours(1),
        }).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void ZeroOrNegativeSessionMaxAge_Fails(long ticks)
    {
        var result = Sut.Validate(null, new AuthOptions { SessionMaxAge = TimeSpan.FromTicks(ticks) });

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle()
            .Which.Should().Contain("Auth:SessionMaxAge");
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void ZeroOrNegativeSessionCleanupInterval_Fails(long ticks)
    {
        var result = Sut.Validate(null, new AuthOptions { SessionCleanupInterval = TimeSpan.FromTicks(ticks) });

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle()
            .Which.Should().Contain("Auth:SessionCleanupInterval");
    }

    [Fact]
    public void BothValuesBad_ReportsBothFailures()
    {
        var result = Sut.Validate(null, new AuthOptions
        {
            SessionMaxAge = TimeSpan.Zero,
            SessionCleanupInterval = TimeSpan.FromTicks(-1),
        });

        result.Failures.Should().HaveCount(2);
    }
}
