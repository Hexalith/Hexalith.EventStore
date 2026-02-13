namespace Hexalith.EventStore.CommandApi.Authentication;

using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;

/// <summary>
/// Transforms JWT claims into normalized eventstore:* claims for downstream authorization.
/// Extracts tenant, domain, and permission information from JWT custom claims.
/// </summary>
public class EventStoreClaimsTransformation(ILogger<EventStoreClaimsTransformation> logger) : IClaimsTransformation
{
    internal const string TenantClaimType = "eventstore:tenant";
    internal const string DomainClaimType = "eventstore:domain";
    internal const string PermissionClaimType = "eventstore:permission";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // Idempotency: if any eventstore:* claims already exist, skip transformation
        if (principal.HasClaim(c => c.Type is TenantClaimType or DomainClaimType or PermissionClaimType))
        {
            return Task.FromResult(principal);
        }

        var identity = new ClaimsIdentity();

        AddTenantClaims(principal, identity);
        AddClaimsFromJwt(principal, identity, "domains", DomainClaimType);
        AddClaimsFromJwt(principal, identity, "permissions", PermissionClaimType);

        if (identity.Claims.Any())
        {
            principal.AddIdentity(identity);
        }

        string subject = principal.FindFirst("sub")?.Value ?? "unknown";
        int tenantCount = identity.Claims.Count(c => c.Type == TenantClaimType);
        int domainCount = identity.Claims.Count(c => c.Type == DomainClaimType);

        logger.LogDebug(
            "Claims transformation for Subject={Subject}: Tenants={TenantCount}, Domains={DomainCount}",
            subject,
            tenantCount,
            domainCount);

        return Task.FromResult(principal);
    }

    private void AddTenantClaims(ClaimsPrincipal principal, ClaimsIdentity identity)
    {
        // Try "tenants" claim (array or space-delimited)
        AddClaimsFromJwt(principal, identity, "tenants", TenantClaimType);

        // Also support singular "tenant_id" or "tid" claims
        string? tenantId = principal.FindFirst("tenant_id")?.Value ?? principal.FindFirst("tid")?.Value;
        if (!string.IsNullOrEmpty(tenantId) && !identity.HasClaim(TenantClaimType, tenantId))
        {
            identity.AddClaim(new Claim(TenantClaimType, tenantId));
        }
    }

    private void AddClaimsFromJwt(ClaimsPrincipal principal, ClaimsIdentity identity, string sourceClaimType, string targetClaimType)
    {
        Claim? sourceClaim = principal.FindFirst(sourceClaimType);
        if (sourceClaim is null)
        {
            return;
        }

        string value = sourceClaim.Value;

        // Try parsing as JSON array first
        if (value.StartsWith('['))
        {
            try
            {
                string[]? items = JsonSerializer.Deserialize<string[]>(value);
                if (items is not null)
                {
                    foreach (string item in items)
                    {
                        if (!string.IsNullOrEmpty(item))
                        {
                            identity.AddClaim(new Claim(targetClaimType, item));
                        }
                    }

                    return;
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(
                    "Failed to parse claim '{ClaimType}' as JSON array, falling back to space-delimited parsing. Error: {Error}",
                    sourceClaimType,
                    ex.Message);
            }
        }

        // Parse as space-delimited string
        string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            identity.AddClaim(new Claim(targetClaimType, part));
        }
    }
}
