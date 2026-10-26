using FluentAssertions;
using Serilog.Events;
using TripExpenseTracker.Api.Logging;

namespace TripExpenseTracker.Tests.Logging;

public class SensitiveDataRedactorTests
{
    private const string SecretValue = "secret-value-123";

    [Theory]
    [InlineData("password")]
    [InlineData("passwordHash")]
    [InlineData("sessionId")]
    [InlineData("token")]
    [InlineData("cookie")]
    [InlineData("authorization")]
    public void TopLevelSensitiveProperty_IsMasked(string name)
    {
        var logEvent = Event(Scalar(name, SecretValue));

        SensitiveDataRedactor.Redact(logEvent);

        AssertMasked(logEvent.Properties[name]);
    }

    [Theory]
    [InlineData("PASSWORD")]
    [InlineData("PasswordHash")]
    [InlineData("SessionId")]
    [InlineData("TOKEN")]
    [InlineData("Cookie")]
    [InlineData("AUTHORIZATION")]
    public void SensitivePropertyName_IsMaskedRegardlessOfCase(string name)
    {
        var logEvent = Event(Scalar(name, SecretValue));

        SensitiveDataRedactor.Redact(logEvent);

        AssertMasked(logEvent.Properties[name]);
    }

    [Fact]
    public void SensitivePropertyInsideStructuredValue_IsMasked_AndOtherFieldsAreKept()
    {
        var user = new StructureValue(
        [
            Scalar("SessionId", SecretValue),
            Scalar("Email", "anna@x.com"),
            Scalar("Password", SecretValue),
        ]);
        var logEvent = Event(new LogEventProperty("user", user));

        SensitiveDataRedactor.Redact(logEvent);

        var nested = ((StructureValue)logEvent.Properties["user"]).Properties.ToDictionary(p => p.Name, p => p.Value);
        AssertMasked(nested["SessionId"]);
        AssertMasked(nested["Password"]);
        ((ScalarValue)nested["Email"]).Value.Should().Be("anna@x.com");
    }

    [Fact]
    public void SensitivePropertyInsideSequenceOfStructures_IsMasked()
    {
        var items = new SequenceValue(
        [
            new StructureValue([Scalar("token", SecretValue)]),
        ]);
        var logEvent = Event(new LogEventProperty("items", items));

        SensitiveDataRedactor.Redact(logEvent);

        var first = ((StructureValue)((SequenceValue)logEvent.Properties["items"]).Elements[0]).Properties.Single();
        AssertMasked(first.Value);
    }

    [Fact]
    public void NonSensitiveProperty_IsLeftUnchanged()
    {
        var logEvent = Event(Scalar("Method", "POST"), Scalar("Path", "/api/auth/login"));

        SensitiveDataRedactor.Redact(logEvent);

        ((ScalarValue)logEvent.Properties["Method"]).Value.Should().Be("POST");
        ((ScalarValue)logEvent.Properties["Path"]).Value.Should().Be("/api/auth/login");
    }

    [Fact]
    public void Enrich_MasksThroughTheEnricherEntryPoint()
    {
        var logEvent = Event(Scalar("password", SecretValue));

        new SensitiveDataRedactor().Enrich(logEvent, propertyFactory: null!);

        AssertMasked(logEvent.Properties["password"]);
    }

    private static LogEventProperty Scalar(string name, string value) => new(name, new ScalarValue(value));

    // A fixed timestamp keeps this test independent of the real clock.
    private static LogEvent Event(params LogEventProperty[] properties) =>
        new(DateTimeOffset.UnixEpoch, LogEventLevel.Information, null,
            new MessageTemplate("test", Array.Empty<Serilog.Parsing.MessageTemplateToken>()), properties);

    private static void AssertMasked(LogEventPropertyValue value)
    {
        value.Should().BeOfType<ScalarValue>();
        ((ScalarValue)value).Value.Should().Be(SensitiveDataRedactor.Mask);
    }
}
