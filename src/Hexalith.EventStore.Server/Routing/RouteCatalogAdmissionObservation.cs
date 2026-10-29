using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Routing;

/// <summary>Captures installed consumer adapters and nonsecret configured key policy, proving each required key generation is available.</summary>
public static class RouteCatalogAdmissionObservation
{
    /// <summary>Reads actual service registrations and providers without accepting proposed catalog content.</summary>
    public static async Task<IReadOnlyList<IdempotencyCatalogEntry>> CaptureAsync(IServiceProvider services, string consumerAppId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerAppId);
        var adapters = services.GetServices<IIdempotencyIntentAdapter>().ToArray();
        if (adapters.Length == 0) return [];
        var options = services.GetRequiredService<IOptions<IdempotencyAdmissionOptions>>().Value;
        if (!options.Enabled) throw new RouteCatalogUnavailableException("Installed admission consumers require enabled policy.");
        using var ring = await services.GetRequiredService<IIdempotencyDigestKeyProvider>().GetKeyRingAsync(cancellationToken).ConfigureAwait(false);
        if (ring.ActiveVersion != options.ActiveDigestKeyVersion
            || !ring.ReaderVersions.SequenceEqual(options.ReaderDigestKeyVersions, StringComparer.Ordinal))
            throw new RouteCatalogUnavailableException("Available digest generations disagree with configured policy.");
        var facts = new List<IdempotencyCatalogEntry>();
        foreach (var adapter in adapters)
        {
            if (adapter is not IDeclaredIdempotencyIntentAdapter declared || adapter.CommandType != declared.Declaration.Route.MessageType
                || adapter.AdapterId != declared.Declaration.AdapterId || adapter.OperationId != declared.Declaration.OperationId
                || adapter.DescriptorVersion != declared.Declaration.DescriptorSchema.Version || adapter.RetentionTier != declared.Declaration.RetentionTier)
                throw new RouteCatalogUnavailableException("Installed admission adapter lacks a matching complete static declaration.");
            var policy = declared.Declaration;
            var entry = new IdempotencyCatalogEntry(policy.Route.RouteId, 1, policy.Route.Domain, policy.Route.MessageType!,
                adapter.AdapterId, adapter.OperationId, policy.PolicyVersion, policy.DescriptorSchema,
                RouteCatalogCodec.ComputeDescriptorDigest(policy.DescriptorSchema), policy.RetentionTier.ToString().ToLowerInvariant(),
                ring.ActiveVersion, ring.Versions, options.DigestKeySecretStoreName, options.DigestKeySecretName,
                options.DigestKeySecretGeneration, ring.Versions.Select(version => new DigestKeyMapReference(version, version)).ToArray(), consumerAppId, string.Empty);
            facts.Add(entry with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(entry) });
        }
        if (facts.Select(f => f.RouteId).Distinct(StringComparer.Ordinal).Count() != facts.Count)
            throw new RouteCatalogUnavailableException("Installed admission adapter identities are ambiguous.");
        return facts.AsReadOnly();
    }
}
