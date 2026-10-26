using System.Net;
using System.Text.Json;
using TripExpenseTracker.IntegrationTests.Support;

namespace TripExpenseTracker.IntegrationTests.StaticFiles;

/// <summary>
/// The API serves the built Angular app in production (F1.T14). Non-API paths get index.html so the Angular
/// router can handle them. Unknown /api paths stay API 404s with ProblemDetails, never index.html.
/// </summary>
public class StaticFileServingTests : IClassFixture<StaticWebApplicationFactory>
{
    private readonly StaticWebApplicationFactory _factory;

    public StaticFileServingTests(StaticWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NonApiPath_ReturnsIndexHtml()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/trips");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(StaticWebApplicationFactory.StubIndexHtml, body);
    }

    [Fact]
    public async Task Root_ReturnsIndexHtml()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(StaticWebApplicationFactory.StubIndexHtml, body);
    }

    [Fact]
    public async Task UnknownApiPath_Returns404ProblemDetails_NotIndexHtml()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/nope");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("<app-root>", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthRoute_StillReturnsOk_WithStaticFilesEnabled()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
