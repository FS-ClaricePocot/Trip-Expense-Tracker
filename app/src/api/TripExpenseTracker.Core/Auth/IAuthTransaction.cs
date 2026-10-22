namespace TripExpenseTracker.Core.Auth;

/// <summary>
/// Runs work as one database transaction (F1.R7). Core cannot reference the Data project, so
/// TripExpenseTracker.Data implements this over its unit of work. The DAOs used inside <c>work</c>
/// must share that same unit of work.
/// </summary>
public interface IAuthTransaction
{
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}
