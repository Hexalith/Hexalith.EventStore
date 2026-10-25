using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Binds every command, current-state byte and complete-source field to the authenticated platform invocation.</summary>
public static class CommandSourceAuthorityBinding
{
    /// <summary>The signed complete-source request digest claim.</summary>
    public const string DigestClaim = EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix + "complete-command-source-v1";
    /// <summary>The exact source actor binding.</summary>
    public const string SourceClaim = EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix + "command-source";
    /// <summary>The enrolled current source epoch binding.</summary>
    public const string EpochClaim = EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix + "command-source-epoch";
    /// <summary>The exact method binding.</summary>
    public const string MethodClaim = EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix + "command-source-method";
    /// <summary>A platform-only outbound request option; wire fields cannot populate this option.</summary>
    public static readonly HttpRequestOptionsKey<IReadOnlyDictionary<string, string>> RequestBindings = new("Hexalith.CompleteCommandSourceBindings");

    /// <summary>Captures the exact platform-created invocation bindings.</summary>
    public static IReadOnlyDictionary<string, string> Bind(DomainServiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CompleteSource is null) throw new ArgumentException("A complete source is required.", nameof(request));
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = request.Command.TenantId,
            [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = request.Command.Domain,
            [SourceClaim] = request.CompleteSource.Identity.ActorId,
            [EpochClaim] = request.CompleteSource.AuthorityRevision,
            [MethodClaim] = "process",
            // Normalize the object-valued CurrentState through a JSON element on both sides of the wire.
            // This preserves values while making escaping identical for typed dates and received JSON strings.
            [DigestClaim] = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
                JsonSerializer.SerializeToElement(request, JsonSerializerOptions.Web), JsonSerializerOptions.Web))),
        };
    }

    /// <summary>Checks exact request bindings against the minimal already authenticated workload principal.</summary>
    public static bool Verify(DomainServiceRequest request, ClaimsPrincipal workload, string platformWorkloadId)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(workload);
        if (request.CompleteSource is null || string.IsNullOrWhiteSpace(platformWorkloadId)) return false;
        var identities = workload.Identities.Where(identity => identity.IsAuthenticated
            && identity.AuthenticationType == EventStoreWorkloadAuthenticationDefaults.WorkloadScheme).ToArray();
        if (identities.Length != 1) return false;
        var principal = new ClaimsPrincipal(identities[0]);
        Claim[] callers = principal.FindAll(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType).ToArray();
        Claim[] bindings = principal.Claims.Where(claim => claim.Type.StartsWith(EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix, StringComparison.Ordinal)).ToArray();
        var expected = Bind(request);
        return callers.Length == 1 && callers[0].Value == platformWorkloadId
            && principal.FindAll(EventStoreWorkloadAuthenticationDefaults.OperationClaimType).Count(claim => claim.Value == EventStoreWorkloadOperations.DomainServiceProcess) == 1
            && bindings.Length == expected.Count && expected.All(pair => bindings.Count(claim => claim.Type == pair.Key && claim.Value == pair.Value) == 1);
    }
}
