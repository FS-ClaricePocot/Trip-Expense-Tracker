using Serilog.Core;
using Serilog.Events;

namespace TripExpenseTracker.Api.Logging;

/// <summary>
/// Masks log properties that can carry secrets (F1.R3, F1.R11, F1.AC10). Names are matched case-insensitively,
/// at the top level and inside structured (@-destructured) values. Values are replaced with <see cref="Mask"/>,
/// never dropped, so each event keeps its shape. Runs as an enricher, so every sink receives the masked event.
/// </summary>
public sealed class SensitiveDataRedactor : ILogEventEnricher
{
    public const string Mask = "[REDACTED]";

    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "passwordHash",
        "sessionId",
        "token",
        "cookie",
        "authorization",
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) => Redact(logEvent);

    /// <summary>Masks the sensitive properties of one event in place.</summary>
    public static void Redact(LogEvent logEvent)
    {
        foreach (var property in logEvent.Properties.ToList())
        {
            logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, RedactProperty(property.Key, property.Value)));
        }
    }

    private static LogEventPropertyValue RedactProperty(string name, LogEventPropertyValue value) =>
        SensitiveNames.Contains(name) ? new ScalarValue(Mask) : RedactNested(value);

    private static LogEventPropertyValue RedactNested(LogEventPropertyValue value) => value switch
    {
        StructureValue structure => new StructureValue(
            structure.Properties.Select(property => new LogEventProperty(property.Name, RedactProperty(property.Name, property.Value))),
            structure.TypeTag),
        SequenceValue sequence => new SequenceValue(sequence.Elements.Select(RedactNested)),
        _ => value,
    };
}
