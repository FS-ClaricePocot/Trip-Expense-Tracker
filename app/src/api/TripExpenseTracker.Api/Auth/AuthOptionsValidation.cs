using Microsoft.Extensions.Options;
using TripExpenseTracker.Core.Auth;

namespace TripExpenseTracker.Api.Auth;

/// <summary>
/// Startup check for the "Auth" settings. Both durations must be greater than zero, so a zero or negative
/// value stops the app at startup (ValidateOnStart) instead of failing on first use. The failure message names
/// the configuration key only. It never includes the value.
/// </summary>
public sealed class AuthOptionsValidation : IValidateOptions<AuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        var failures = new List<string>();

        if (options.SessionMaxAge <= TimeSpan.Zero)
        {
            failures.Add("Auth:SessionMaxAge must be greater than zero.");
        }

        if (options.SessionCleanupInterval <= TimeSpan.Zero)
        {
            failures.Add("Auth:SessionCleanupInterval must be greater than zero.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
