using DbUp;
using DbUp.Engine.Output;
using Serilog;

namespace TripExpenseTracker.Data;

/// <summary>
/// Runs the versioned SQL scripts embedded in this assembly with DbUp.
/// Each script runs once; scripts already recorded in the journal are skipped.
/// </summary>
public static class MigrationRunner
{
    public static void Migrate(string connectionString, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(logger);

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(MigrationRunner).Assembly)
            .LogTo(new SerilogUpgradeLog(logger))
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            // The DbUp error is kept as the inner exception; it is not logged here because it can echo SQL.
            throw new InvalidOperationException("Database migration failed.", result.Error);
        }
    }

    private sealed class SerilogUpgradeLog(ILogger logger) : IUpgradeLog
    {
        public void LogTrace(string format, params object[] args) =>
            logger.Verbose("{Message}", string.Format(format, args));

        public void LogDebug(string format, params object[] args) =>
            logger.Debug("{Message}", string.Format(format, args));

        public void LogInformation(string format, params object[] args) =>
            logger.Information("{Message}", string.Format(format, args));

        public void LogWarning(string format, params object[] args) =>
            logger.Warning("{Message}", string.Format(format, args));

        public void LogError(string format, params object[] args) =>
            logger.Error("{Message}", string.Format(format, args));

        public void LogError(Exception ex, string format, params object[] args) =>
            logger.Error(ex, "{Message}", string.Format(format, args));
    }
}
