namespace TripExpenseTracker.Core.Auth;

/// <summary>A registered account. Email is trimmed and lower-cased (F1.R2, F1.R13).</summary>
public sealed record User(Guid Id, string Email, string PasswordHash, DateTimeOffset CreatedAt);
