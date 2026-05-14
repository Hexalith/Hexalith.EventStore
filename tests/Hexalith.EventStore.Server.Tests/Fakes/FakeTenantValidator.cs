
using System.Collections.Concurrent;
using System.Security.Claims;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Authorization;

namespace Hexalith.EventStore.Server.Tests.Fakes;

/// <summary>
/// Fake tenant validator for unit testing. Default-deny — tests must opt in by setting ConfiguredResult.
/// </summary>
public class FakeTenantValidator : ITenantValidator {
    private readonly ConcurrentQueue<(ClaimsPrincipal User, string TenantId, string? AggregateId)> _receivedRequests = new();

    /// <summary>Gets the list of received validation calls for assertion.</summary>
    public IReadOnlyCollection<(ClaimsPrincipal User, string TenantId, string? AggregateId)> ReceivedRequests => [.. _receivedRequests];

    /// <summary>Gets or sets the result to return. Defaults to denied.</summary>
    public TenantValidationResult? ConfiguredResult { get; set; }

    /// <summary>Clears recorded calls and resets to default-deny.</summary>
    public void Reset() {
        _receivedRequests.Clear();
        ConfiguredResult = null;
    }

    /// <inheritdoc/>
    public Task<TenantValidationResult> ValidateAsync(
        ClaimsPrincipal user,
        string tenantId,
        CancellationToken cancellationToken,
        string? aggregateId = null) {
        ArgumentNullException.ThrowIfNull(user);
        _receivedRequests.Enqueue((user, tenantId, aggregateId));
        return Task.FromResult(ConfiguredResult ?? TenantValidationResult.Denied(
            "Test fake: no result configured. Authorization denied by default.",
            AuthorizationFailureReason.PrincipalNotMember));
    }
}
