using System.Security.Claims;
using Hexalith.EventStore.Contracts.Queries;
using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.DomainService;

/// <summary>Request-scoped originating caller verified against the exact workload-authenticated query.</summary>
public sealed class VerifiedQueryCaller
{
    private static readonly object FeatureKey = new();
    private VerifiedQueryCaller(QueryEnvelope query, QueryEnvelope originalQuery)
    {
        Query = query;
        OriginalQuery = originalQuery;
        List<Claim> claims = [new("sub", query.UserId), new(ClaimTypes.NameIdentifier, query.UserId), new("tenantId", query.TenantId),
            new("eventstore:current-tenant", query.TenantId)];
        claims.AddRange(query.CallerAuthority!.Roles.Select(static role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(query.CallerAuthority.Permissions.Select(static permission => new Claim("eventstore:permission", permission)));
        Principal = new(new ClaimsIdentity(claims, "VerifiedQueryCaller", "sub", ClaimTypes.Role));
    }
    /// <summary>The exact verified query and its originating identity fields.</summary>
    public QueryEnvelope Query { get; }
    /// <summary>The original wire query whose exact digest was verified, before local administrator revalidation.</summary>
    public QueryEnvelope OriginalQuery { get; }
    /// <summary>Verified caller claims, separate from the transport workload principal.</summary>
    public ClaimsPrincipal Principal { get; }
    /// <summary>Gets the caller established by the SDK dispatcher for this request, or null.</summary>
    public static VerifiedQueryCaller? Get(HttpContext? context)
        => context?.Items.TryGetValue(FeatureKey, out object? value) == true ? value as VerifiedQueryCaller : null;
    internal static void Set(HttpContext context, QueryEnvelope query, QueryEnvelope originalQuery) => context.Items[FeatureKey] = new VerifiedQueryCaller(query, originalQuery);
    internal static void Clear(HttpContext context) => context.Items.Remove(FeatureKey);
}
