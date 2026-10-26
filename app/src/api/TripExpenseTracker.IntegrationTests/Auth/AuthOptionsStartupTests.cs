using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace TripExpenseTracker.IntegrationTests.Auth;

/// <summary>
/// Startup validation of the "Auth" section (ValidateOnStart). Each test builds a real host with one bad value
/// in configuration and checks that creating the host fails. No database is needed: the factory clears the
/// connection string and migrations stay off, like <see cref="Support.TestWebApplicationFactory"/>.
/// </summary>
public class AuthOptionsStartupTests
{
    [Fact]
    public async Task ValidAuthSettings_StartTheHost_AndHealthResponds()
    {
        using var factory = new AuthSettingsFactory(new Dictionary<string, string?>());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void MalformedSessionMaxAge_FailsStartup()
    {
        using var factory = new AuthSettingsFactory(new Dictionary<string, string?>
        {
            ["Auth:SessionMaxAge"] = "not-a-duration",
        });

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("SessionMaxAge", ex.ToString());
    }

    [Fact]
    public void ZeroSessionCleanupInterval_FailsStartupWithValidationError()
    {
        using var factory = new AuthSettingsFactory(new Dictionary<string, string?>
        {
            ["Auth:SessionCleanupInterval"] = "00:00:00",
        });

        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Auth:SessionCleanupInterval must be greater than zero.", ex.Message);
    }

    /// <summary>Starts the API with the given Auth overrides on top of appsettings.json. No database is used.</summary>
    private sealed class AuthSettingsFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> _authOverrides;

        public AuthSettingsFactory(Dictionary<string, string?> authOverrides)
        {
            _authOverrides = authOverrides;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["Database:MigrateOnStartup"] = "false",
                    ["ConnectionStrings:Default"] = string.Empty,
                    ["Auth:SessionCleanupEnabled"] = "false",
                };
                foreach (var pair in _authOverrides)
                {
                    values[pair.Key] = pair.Value;
                }

                config.AddInMemoryCollection(values);
            });
        }
    }
}
