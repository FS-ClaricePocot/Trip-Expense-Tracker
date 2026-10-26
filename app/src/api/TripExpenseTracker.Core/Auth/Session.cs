namespace TripExpenseTracker.Core.Auth;

/// <summary>A server-side session. Id is the random value held in the browser cookie (F1.R6).</summary>
public sealed record Session(string Id, Guid UserId, DateTimeOffset CreatedAt);
