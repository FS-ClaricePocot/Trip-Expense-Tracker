using Microsoft.AspNetCore.Mvc;
using TripExpenseTracker.Core.Auth;

namespace TripExpenseTracker.IntegrationTests.Support;

/// <summary>An in-memory record for the ownership probe. Test-only; it is never part of the shipped Api.</summary>
public sealed record ProbeRecord(int Id, Guid OwnerId, string Name);

/// <summary>
/// Holds the probe's records. <see cref="DatabaseWebApplicationFactory"/> registers one instance as a singleton,
/// so each test factory has its own records and tests do not depend on each other.
/// </summary>
public sealed class OwnershipProbeStore
{
    public const string OwnedName = "Lisbon";

    public List<ProbeRecord> Records { get; } = new();

    /// <summary>Adds record 1, owned by the given user. Call it after registering that user.</summary>
    public void SeedOwnedBy(Guid ownerId) => Records.Add(new ProbeRecord(1, ownerId, OwnedName));
}

/// <summary>
/// Test-only stand-in for a trip endpoint (F1.R9). F1 has no trip table, so this serves an in-memory record.
/// It uses the same ownership pattern as F2 (see <see cref="ICurrentUser"/>): the owner id is part of the lookup,
/// so "not yours" and "missing" both find nothing and return the same 404. It has no [AllowAnonymous], so the
/// fallback policy protects it (F1.R1).
/// </summary>
[ApiController]
[Route("api/test/ownership")]
public sealed class OwnershipProbeController : ControllerBase
{
    private readonly OwnershipProbeStore _store;
    private readonly ICurrentUser _currentUser;

    public OwnershipProbeController(OwnershipProbeStore store, ICurrentUser currentUser)
    {
        _store = store;
        _currentUser = currentUser;
    }

    [HttpGet("{id:int}")]
    public IActionResult Get(int id)
    {
        var userId = _currentUser.UserId;
        var record = _store.Records.FirstOrDefault(r => r.Id == id && r.OwnerId == userId);
        return record is null ? NotFound() : Ok(new { record.Id, record.Name });
    }
}
