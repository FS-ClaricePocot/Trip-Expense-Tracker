using Microsoft.Extensions.Options;
using TripExpenseTracker.Core.Auth;

namespace TripExpenseTracker.Api.Auth;

/// <summary>
/// Deletes sessions older than <see cref="AuthOptions.SessionMaxAge"/> every <see cref="AuthOptions.SessionCleanupInterval"/> (F1.R6).
/// The DAO is scoped, but this service is a singleton, so each run opens its own scope. A failed run is logged
/// and the loop waits for the next interval. Only the number of removed rows is logged, never a session ID.
/// </summary>
public sealed class SessionCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly AuthOptions _options;
    private readonly ILogger<SessionCleanupService> _logger;

    public SessionCleanupService(
        IServiceScopeFactory scopes,
        TimeProvider time,
        IOptions<AuthOptions> options,
        ILogger<SessionCleanupService> logger)
    {
        _scopes = scopes;
        _time = time;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(_options.SessionCleanupInterval, _time, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown: the loop ends.
            }
        }
    }

    /// <summary>
    /// One cleanup pass. Any failure except shutdown is logged here, so the caller (the loop or a test) keeps going.
    /// </summary>
    public async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = _time.GetUtcNow() - _options.SessionMaxAge;
            await using var scope = _scopes.CreateAsyncScope();
            var sessions = scope.ServiceProvider.GetRequiredService<ISessionDao>();
            var removed = await sessions.DeleteOlderThanAsync(cutoff, cancellationToken);

            _logger.LogInformation("Expired sessions removed: {Removed}", removed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Expired-session cleanup failed. It will retry in {Interval}.", _options.SessionCleanupInterval);
        }
    }
}
