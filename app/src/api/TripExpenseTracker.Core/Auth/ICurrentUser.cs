namespace TripExpenseTracker.Core.Auth;

/// <summary>
/// The logged-in user for the current request (F1.R9). Implemented in TripExpenseTracker.Api from the authenticated principal.
/// </summary>
/// <remarks>
/// Ownership pattern that F2 and later features follow (F1.R9, CLAUDE.md):
/// the owner id goes in the DAO's WHERE clause, for example <c>WHERE trip_id = @trip AND user_id = @user</c>.
/// A record that exists but belongs to someone else and a record that does not exist both match no row,
/// so both return the same 404 with the same body. Never check ownership in a second query or after the fact,
/// because that can leak whether the record exists.
/// </remarks>
public interface ICurrentUser
{
    /// <summary>The id of the logged-in user. Only valid on endpoints that require a session.</summary>
    Guid UserId { get; }
}
