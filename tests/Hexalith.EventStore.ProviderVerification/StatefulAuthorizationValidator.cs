using System.Security.Claims;

using Hexalith.EventStore.Authorization;

namespace Hexalith.EventStore.ProviderVerification;

internal sealed class StatefulAuthorizationValidator(ProviderStateCoordinator coordinator)
    : ITenantValidator, IRbacValidator
{
    public Task<TenantValidationResult> ValidateAsync(
        ClaimsPrincipal user,
        string tenantId,
        CancellationToken cancellationToken,
        string? aggregateId = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string state = SupportedProviderStates.RequireActive(coordinator);
        bool forbidden = state is "command-forbidden" or "query-forbidden";
        string expectedTenant = state is "tenant-mismatch" or "query-auth-tenant"
            ? "Tenant_Contract_Case"
            : "tenant-contract-a";
        bool claimsMatch = user.HasClaim("sub", "user-contract-a")
            && user.HasClaim("eventstore:tenant", expectedTenant)
            && string.Equals(tenantId, expectedTenant, StringComparison.Ordinal)
            && (aggregateId is null || string.Equals(aggregateId, "order-1", StringComparison.Ordinal));
        return Task.FromResult(forbidden || !claimsMatch
            ? TenantValidationResult.Denied("Provider verification policy denied the tenant.")
            : TenantValidationResult.Allowed);
    }

    public Task<RbacValidationResult> ValidateAsync(
        ClaimsPrincipal user,
        string tenantId,
        string domain,
        string messageType,
        string messageCategory,
        CancellationToken cancellationToken,
        string? aggregateId = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string state = SupportedProviderStates.RequireActive(coordinator);
        bool forbidden = state is "command-forbidden" or "query-forbidden";
        string expectedTenant = state is "tenant-mismatch" or "query-auth-tenant"
            ? "Tenant_Contract_Case"
            : "tenant-contract-a";
        string permission = messageCategory switch
        {
            "command" => "command:*",
            "query" => "query:*",
            _ => throw new InvalidOperationException("provider-authorization-category-unsupported"),
        };
        bool messageMatches = messageCategory == "command"
            ? messageType.EndsWith("ShipOrderCommand", StringComparison.Ordinal)
            : string.Equals(messageType, "GetOrders", StringComparison.Ordinal);
        bool claimsMatch = !forbidden
            && user.HasClaim("sub", "user-contract-a")
            && user.HasClaim("eventstore:tenant", expectedTenant)
            && user.HasClaim("eventstore:domain", "orders")
            && user.HasClaim("eventstore:permission", permission)
            && string.Equals(tenantId, expectedTenant, StringComparison.Ordinal)
            && string.Equals(domain, "orders", StringComparison.Ordinal)
            && (aggregateId is null || string.Equals(aggregateId, "order-1", StringComparison.Ordinal))
            && messageMatches;
        return Task.FromResult(claimsMatch
            ? RbacValidationResult.Allowed
            : RbacValidationResult.Denied("Provider verification claims did not match the request."));
    }
}
