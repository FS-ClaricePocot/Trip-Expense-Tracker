using Serilog;
using TripExpenseTracker.Core.Auth;

namespace TripExpenseTracker.Data;

/// <summary>
/// Runs auth work as one database transaction (F1.R7) over the unit of work. The DAOs used inside the work
/// must be built on the same unit of work, so they share its transaction. Scoped per request.
/// </summary>
public sealed class AuthTransaction : IAuthTransaction
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger _logger;

    public AuthTransaction(IUnitOfWork unitOfWork, ILogger logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginAsync(cancellationToken);
        try
        {
            var result = await work(cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            // Rollback uses CancellationToken.None so a cancelled request still rolls back, then the original error is rethrown.
            // A failed rollback is logged by type only and must not replace the original error. The bare throw
            // below rethrows the outer exception with its original stack.
            try
            {
                await _unitOfWork.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackError)
            {
                _logger.Error("Auth transaction rollback failed with {ExceptionType}; rethrowing the original error", rollbackError.GetType().Name);
            }

            throw;
        }
    }
}
