using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace TripExpenseTracker.IntegrationTests.Support;

/// <summary>
/// An in-memory Serilog sink. The API adds it through DI, so a test reads the events the real pipeline
/// wrote after redaction (F1.AC10).
/// </summary>
public sealed class InMemoryLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    public IReadOnlyList<LogEvent> Snapshot() => _events.ToArray();
}
