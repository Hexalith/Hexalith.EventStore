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
        return new Dictionary<string, string>(StringComparer.Ordinal) { [DigestClaim] = Digest(query) };
    }
    /// <summary>Validates the exact query against the minimal principal produced by workload authentication.</summary>
    public static bool Verify(QueryEnvelope query, ClaimsPrincipal workload, string gatewayWorkloadId)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(workload);
        Claim[] binding = workload.FindAll(DigestClaim).ToArray();
        Claim[] callers = workload.FindAll(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType).ToArray();
        return workload.Identity?.IsAuthenticated == true && callers.Length == 1 && callers[0].Value == gatewayWorkloadId
            && workload.HasClaim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, EventStoreWorkloadOperations.DomainServiceQuery)
            && binding.Length == 1 && binding[0].Value == Digest(query)
            && query.OriginalActorId == query.UserId && !string.IsNullOrWhiteSpace(query.UserId)
            && query.CallerAuthority is { Origin: "authenticated-caller" or "bound-owner-query" or "delegated-owner-query" } authority
            && Valid(authority.Roles) && Valid(authority.Permissions)
            && (authority.Origin != "delegated-owner-query" || authority.Roles.Count == 0 && query.IsDelegated)
            && (authority.Origin != "bound-owner-query" || authority.Roles.Count == 0 && authority.Permissions.Count == 0);
    }
    private static bool Valid(IReadOnlyList<string>? values) => values is { Count: <= 64 }
        && values.All(static value => !string.IsNullOrWhiteSpace(value) && value.Length <= 512)
        && values.Distinct(StringComparer.Ordinal).Count() == values.Count;
    private static string Digest(QueryEnvelope query)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(query, JsonSerializerOptions.Web)));
}
