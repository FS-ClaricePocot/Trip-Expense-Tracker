namespace TripExpenseTracker.Api.Auth;

/// <summary>Request body for register and login (Contract changes).</summary>
public sealed record CredentialsRequest(string? Email, string? Password)
{
    // Keeps the password out of any log line that prints the request.
    public override string ToString() => $"CredentialsRequest {{ Email = {Email}, Password = [redacted] }}";
}

/// <summary>Response body for register, login and me (Contract changes).</summary>
public sealed record UserResponse(Guid Id, string Email);
