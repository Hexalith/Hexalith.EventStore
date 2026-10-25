using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Canonical exact actor-request bindings using the existing workload assertion contract.</summary>
public static class SourcePublicationWorkloadBinding
{
    /// <summary>The authority journal workload operation.</summary>
    public const string AuthorityOperation = "eventstore:source-publication-authority";
    /// <summary>The primary namespace/index workload operation.</summary>
    public const string PrimaryOperation = "eventstore:source-publication";
    /// <summary>The dynamic exact-body digest claim.</summary>
    public const string DigestClaim = EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix + "publication-body";
    /// <summary>Produces method, exact scope, epoch and byte-digest bindings.</summary>
    public static IReadOnlyDictionary<string, string> Bind(SourcePublicationAuthorityCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        const string prefix = EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix;
        if (call.Body is not { Length: > 0 and <= 33554432 } || string.IsNullOrWhiteSpace(call.Method) || call.Method.Length > 128
            || string.IsNullOrWhiteSpace(call.Epoch) || call.Epoch.Length > 200 || call.Scope is null)
            throw new ArgumentException("Malformed publication invocation.", nameof(call));
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = call.Scope.Tenant,
            [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = call.Scope.Domain,
            [prefix + "publication-scope"] = call.Scope.ActorId,
            [prefix + "publication-method"] = call.Method,
            [prefix + "publication-epoch"] = call.Epoch,
            [DigestClaim] = Convert.ToHexString(SHA256.HashData(call.Body)),
        };
    }
    /// <summary>Requires the complete exact binding set with no extra or missing values.</summary>
    public static bool Verify(SourcePublicationAuthorityCall call, WorkloadAssertionEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        var expected = Bind(call);
        return evaluation.Succeeded && evaluation.Bindings.Count == expected.Count
            && expected.All(pair => evaluation.Bindings.TryGetValue(pair.Key, out string? value) && value == pair.Value);
    }
}
