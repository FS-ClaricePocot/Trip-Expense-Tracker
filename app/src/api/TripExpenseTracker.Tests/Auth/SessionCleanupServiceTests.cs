using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TripExpenseTracker.Api.Auth;
using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Tests.Support;
using Xunit;

namespace TripExpenseTracker.Tests.Auth;

/// <summary>
/// The background cleanup loop (F1.R6). The clock is a <see cref="FakeTimeProvider"/>. The wait between runs is
/// a fake timer that fires only when the test advances the clock, so no test depends on the real time.
/// </summary>
public class SessionCleanupServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    [Fact]
    public async Task ErrorInOneRun_IsLogged_AndTheLoopRunsAgain()
    {
        var dao = new ScriptedSessionDao(
            run => run == 1 ? throw new InvalidOperationException("database unavailable") : 0);
        var time = new FakeTimeProvider(Start);
        var logger = new CapturingLogger();
        var sut = NewService(dao, time, logger);

        await sut.StartAsync(CancellationToken.None);
        await time.TimerCreated;            // the first run failed and the loop is now waiting
        time.Advance(Interval);             // the interval has passed, so the loop runs again
        await dao.SecondRunDone;
        await sut.StopAsync(CancellationToken.None);

        dao.Runs.Should().Be(2);
        logger.Entries.Should().Contain(e =>
            e.Level == LogLevel.Error && e.Exception is InvalidOperationException && e.Message.Contains("cleanup failed"));
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Information && e.Message.Contains("removed"));
    }

    [Fact]
    public async Task EachRun_UsesCutoffOneMaxAgeBeforeNow()
    {
        var dao = new ScriptedSessionDao(_ => 3);
        var sut = NewService(dao, new FakeTimeProvider(Start), new CapturingLogger());

        await sut.CleanupOnceAsync(CancellationToken.None);

        dao.Cutoffs.Should().ContainSingle().Which.Should().Be(Start - TimeSpan.FromHours(24));
    }

    private static SessionCleanupService NewService(ScriptedSessionDao dao, TimeProvider time, ILogger<SessionCleanupService> logger)
    {
        var services = new ServiceCollection();
        services.AddScoped<ISessionDao>(_ => dao);
        var provider = services.BuildServiceProvider();

        return new SessionCleanupService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            time,
            Options.Create(new AuthOptions { SessionCleanupInterval = Interval }),
            logger);
    }

    /// <summary>Session DAO whose result is chosen per run. Run 2 completes <see cref="SecondRunDone"/>.</summary>
    private sealed class ScriptedSessionDao : ISessionDao
    {
        private readonly Func<int, int> _onRun;
        private readonly TaskCompletionSource _secondRun = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<DateTimeOffset> _cutoffs = new();

        public ScriptedSessionDao(Func<int, int> onRun) => _onRun = onRun;

        public int Runs { get; private set; }

        public IReadOnlyList<DateTimeOffset> Cutoffs => _cutoffs;

        public Task SecondRunDone => _secondRun.Task;

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
        {
            _cutoffs.Add(cutoff);
            Runs++;
            var run = Runs;
            if (run == 2)
            {
                _secondRun.TrySetResult();
            }

            return Task.FromResult(_onRun(run));
        }

        public Task InsertAsync(Session session, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Session?> FindAsync(string sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteAsync(string sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);

    private sealed class CapturingLogger : ILogger<SessionCleanupService>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
    }
}
