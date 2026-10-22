using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using TripExpenseTracker.IntegrationTests.Support;
using Xunit;
using static TripExpenseTracker.IntegrationTests.Support.AuthTestHelpers;

namespace TripExpenseTracker.IntegrationTests.Auth;

/// <summary>
/// The ownership pattern (F1.R9, F1.AC1, F1.AC6, F1.AC7) against the test-only probe controller. F2 repeats the
/// AC7 check against the real trip endpoints. Each test registers its own users, so tests do not depend on each other.
/// </summary>
public class OwnershipProbeTests : IClassFixture<PostgresFixture>, IDisposable
{
    private const string GoodPassword = "correct horse battery";
    private const int OwnedId = 1;
    private const int MissingId = 999;

    private readonly DatabaseWebApplicationFactory _factory;

    public OwnershipProbeTests(PostgresFixture db)
    {
        _factory = new DatabaseWebApplicationFactory(db.ConnectionString);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task NoCookie_Returns401_AndNoRecordData()
    {
        using var client = NewClient();
        var owner = await RegisterAsync(client);
        _factory.OwnershipProbe.SeedOwnedBy(owner.Id);

        var response = await client.GetAsync($"/api/test/ownership/{OwnedId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(OwnershipProbeStore.OwnedName, body);
    }

    [Fact]
    public async Task OtherUser_GetsSameResponse_AsForAMissingRecord_AndOwnerStillGetsData()
    {
        using var client = NewClient();
        var owner = await RegisterAsync(client);
        var other = await RegisterAsync(client);
        _factory.OwnershipProbe.SeedOwnedBy(owner.Id);

        var ownerResponse = await client.SendAsync(WithSession(HttpMethod.Get, $"/api/test/ownership/{OwnedId}", owner.SessionId));
        var otherOnOwned = await client.SendAsync(WithSession(HttpMethod.Get, $"/api/test/ownership/{OwnedId}", other.SessionId));
        var otherOnMissing = await client.SendAsync(WithSession(HttpMethod.Get, $"/api/test/ownership/{MissingId}", other.SessionId));
        var otherOnOwnedBody = await otherOnOwned.Content.ReadAsStringAsync();
        var otherOnMissingBody = await otherOnMissing.Content.ReadAsStringAsync();

        // Positive control: the owner really does reach the record, so the 404 below is not a broken endpoint.
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.Contains(OwnershipProbeStore.OwnedName, await ownerResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, otherOnOwned.StatusCode);
        Assert.Equal(otherOnMissing.StatusCode, otherOnOwned.StatusCode);
        Assert.Equal(otherOnMissing.Content.Headers.ContentType?.MediaType, otherOnOwned.Content.Headers.ContentType?.MediaType);
        // traceId is a per-request correlation id, not record data, so it is left out of the comparison.
        Assert.Equal(WithoutTraceId(otherOnMissingBody), WithoutTraceId(otherOnOwnedBody));
        Assert.DoesNotContain(OwnershipProbeStore.OwnedName, otherOnOwnedBody);
    }

    [Fact]
    public async Task AfterLogout_OwnRequest_Returns401()
    {
        using var client = NewClient();
        var owner = await RegisterAsync(client);
        _factory.OwnershipProbe.SeedOwnedBy(owner.Id);

        var before = await client.SendAsync(WithSession(HttpMethod.Get, $"/api/test/ownership/{OwnedId}", owner.SessionId));
        var logout = await client.SendAsync(WithSession(HttpMethod.Post, "/api/auth/logout", owner.SessionId));
        var after = await client.SendAsync(WithSession(HttpMethod.Get, $"/api/test/ownership/{OwnedId}", owner.SessionId));

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    // HandleCookies is off so the client never sends a cookie on its own. Each request carries exactly the session the test picks.
    private HttpClient NewClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private static string WithoutTraceId(string body)
    {
        var node = JsonNode.Parse(body)!.AsObject();
        node.Remove("traceId");
        return node.ToJsonString();
    }

    /// <summary>Registers a new user, which also logs them in. Returns the user id and the session id from the cookie.</summary>
    private static async Task<(Guid Id, string SessionId)> RegisterAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = UniqueEmail(), password = GoodPassword });
        var user = await response.Content.ReadFromJsonAsync<UserBody>();
        return (user!.Id, SessionIdFrom(response));
    }
}
