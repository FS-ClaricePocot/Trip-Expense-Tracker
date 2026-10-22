namespace TripExpenseTracker.Core.Auth;

/// <summary>Data access for server-side sessions. Implemented with Dapper in TripExpenseTracker.Data.</summary>
public interface ISessionDao
{
    Task InsertAsync(Session session, CancellationToken cancellationToken);

    Task<Session?> FindAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Deletes the session. Does nothing when it is already gone (F1.R5).</summary>
    Task DeleteAsync(string sessionId, CancellationToken cancellationToken);
}
