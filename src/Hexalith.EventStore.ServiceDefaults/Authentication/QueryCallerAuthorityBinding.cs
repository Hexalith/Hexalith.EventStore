using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Binds gateway-observed authority and every immutable query field to the existing workload assertion.</summary>
public static class QueryCallerAuthorityBinding
{
    /// <summary>The exact-query digest binding carried in the signed assertion.</summary>
    public const string DigestClaim = EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix + "query-authority-v1";
    /// <summary>Platform-only outbound request option; HTTP headers and body fields cannot set it.</summary>
    public static readonly HttpRequestOptionsKey<IReadOnlyDictionary<string, string>> RequestBindings = new("Hexalith.QueryAuthorityBindings");
    /// <summary>Creates bindings for an authority envelope already assembled by the authenticated gateway producer.</summary>
    public static IReadOnlyDictionary<string, string> Bind(QueryEnvelope query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = query.TenantId,
            [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = query.Domain,
            [DigestClaim] = Digest(query),
        };
    }
    /// <summary>Validates the exact query against the minimal principal produced by workload authentication.</summary>
    public static bool Verify(QueryEnvelope query, ClaimsPrincipal workload, string gatewayWorkloadId)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(workload);
        Claim[] callers = workload.FindAll(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType).ToArray();
        Claim[] bindings = workload.Claims.Where(static claim => claim.Type.StartsWith(
            EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix, StringComparison.Ordinal)).ToArray();
        IReadOnlyDictionary<string, string> expected = Bind(query);
        return workload.Identity?.IsAuthenticated == true && callers.Length == 1 && callers[0].Value == gatewayWorkloadId
            && workload.HasClaim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, EventStoreWorkloadOperations.DomainServiceQuery)
            && bindings.Length == expected.Count && expected.All(pair => bindings.Count(claim => claim.Type == pair.Key && claim.Value == pair.Value) == 1)
            && query.OriginalActorId == query.UserId && !string.IsNullOrWhiteSpace(query.UserId)
            && query.CallerAuthority is { Origin: "authenticated-caller" or "bound-owner-query" or "delegated-owner-query" } authority
            && Valid(authority.Roles) && Valid(authority.Permissions)
            && (authority.OriginatingContext is null || authority.Origin == "delegated-owner-query"
                && authority.OriginatingContext.OriginalActorId == query.UserId)
            && (authority.Origin != "delegated-owner-query" || authority.Roles.Count == 0 && query.IsDelegated)
            && (authority.Origin != "bound-owner-query" || authority.Roles.Count == 0 && authority.Permissions.Count == 0);
    }
    private static bool Valid(IReadOnlyList<string>? values) => values is { Count: <= 64 }
        && values.All(static value => !string.IsNullOrWhiteSpace(value) && value.Length <= 512)
        && values.Distinct(StringComparer.Ordinal).Count() == values.Count;
    private static string Digest(QueryEnvelope query)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(query, JsonSerializerOptions.Web)));
}
