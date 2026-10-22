
using System.Collections.Concurrent;
using System.Security.Claims;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Authorization;

namespace Hexalith.EventStore.Server.Tests.Fakes;

/// <summary>
/// Fake RBAC validator for unit testing. Default-deny — tests must opt in by setting ConfiguredResult.
/// </summary>
public class FakeRbacValidator : IRbacValidator {
    private readonly ConcurrentQueue<(ClaimsPrincipal User, string TenantId, string Domain, string MessageType, string MessageCategory, string? AggregateId)> _receivedRequests = new();

    /// <summary>Gets the list of received validation calls for assertion.</summary>
    public IReadOnlyCollection<(ClaimsPrincipal User, string TenantId, string Domain, string MessageType, string MessageCategory, string? AggregateId)> ReceivedRequests =>
        [.. _receivedRequests];

    /// <summary>Gets or sets the result to return. Defaults to denied.</summary>
    public RbacValidationResult? ConfiguredResult { get; set; }

    /// <summary>Clears recorded calls and resets to default-deny.</summary>
    public void Reset() {
        _receivedRequests.Clear();
        ConfiguredResult = null;
    }

    /// <inheritdoc/>
    public Task<RbacValidationResult> ValidateAsync(
        ClaimsPrincipal user,
        string tenantId,
        string domain,
        string messageType,
        string messageCategory,
        CancellationToken cancellationToken,
        string? aggregateId = null) {
        ArgumentNullException.ThrowIfNull(user);
        _receivedRequests.Enqueue((user, tenantId, domain, messageType, messageCategory, aggregateId));
        return Task.FromResult(ConfiguredResult ?? RbacValidationResult.Denied(
            "Test fake: no result configured. Authorization denied by default.",
            AuthorizationFailureReason.InsufficientPermission));
    }
}
