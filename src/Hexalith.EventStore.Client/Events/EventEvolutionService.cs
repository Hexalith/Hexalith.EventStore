using System.Collections.Frozen;
using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Composes exact version validators, adjacent transforms and current deserializers through one registry.</summary>
/// <remarks>
/// This unregistered local composition requires an addressed, readable source from its caller.
/// Complete authoritative catalogs, immutable dependency execution, peer pins and production qualification remain separate gates.
/// Returned application object graphs require their own serializer/state ownership qualification.
/// </remarks>
internal sealed class EventEvolutionService
{
    private readonly EventDomainRegistry _registry;
    private readonly FrozenDictionary<(string Type, int Version), RegisteredEventVersionValidation> _validations;
    private readonly FrozenDictionary<string, RegisteredCurrentEventDeserializer> _deserializers;

    /// <summary>Snapshots a complete supplied event catalog and checks every immutable binding before any catalog callback.</summary>
    internal EventEvolutionService(EventDomainRegistry registry,
        IReadOnlyDictionary<(string Type, int Version), RegisteredEventVersionValidation> validations,
        IReadOnlyDictionary<(string Type, int Version), RegisteredEventUpcaster> upcasters,
        IReadOnlyDictionary<string, RegisteredCurrentEventDeserializer> deserializers)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(validations);
        ArgumentNullException.ThrowIfNull(upcasters);
        ArgumentNullException.ThrowIfNull(deserializers);
        _registry = registry;
        registry.CapabilityLoss.RequireNoObservedLoss();
        _validations = validations.ToFrozenDictionary();
        _deserializers = deserializers.ToFrozenDictionary(StringComparer.Ordinal);
        FrozenDictionary<(string Type, int Version), RegisteredEventUpcaster> transforms = upcasters.ToFrozenDictionary();

        (string Type, int Version)[] versions = registry.Rows.Where(static row => row.Tag == 0x56)
            .Select(static row => (row.GetTextKey(1), row.GetVersionKey(2))).ToArray();
        (string Type, int Version)[] edges = registry.Rows.Where(static row => row.Tag == 0x45)
            .Select(static row => (row.GetTextKey(1), row.GetVersionKey(2))).ToArray();
        string[] currentTypes = registry.Rows.Where(static row => row.Tag == 0x44)
            .Select(static row => row.GetTextKey(1)).ToArray();
        if (_validations.Count != versions.Length || versions.Any(key => !_validations.ContainsKey(key))
            || transforms.Count != edges.Length || edges.Any(key => !transforms.ContainsKey(key))
            || _deserializers.Count != currentTypes.Length || currentTypes.Any(key => !_deserializers.ContainsKey(key))
            || _validations.Values.Any(static binding => binding is null)
            || transforms.Values.Any(static binding => binding is null)
            || _deserializers.Values.Any(static binding => binding is null))
        {
            throw new InvalidOperationException("CapabilityMismatch: supplied event bindings must exactly cover the registry catalog.");
        }

        foreach ((string type, int version) in versions)
        {
            _validations[(type, version)].RequireDescriptor(registry, type, version, CancellationToken.None);
        }
        foreach (string type in currentTypes)
        {
            _deserializers[type].RequireDescriptor(registry, type, CancellationToken.None);
        }

        var executor = new EventUpcastChainExecutor(registry, transforms, ValidateVersion, RequireVersionBindings);
        Resolver = new EventLogicalViewResolver(registry, executor);
        registry.CapabilityLoss.RequireNoObservedLoss();
    }

    /// <summary>Gets the composed resolver for addressed Dapr page preparation and bounded effective resolution.</summary>
    internal EventLogicalViewResolver Resolver { get; }

    /// <summary>Resolves a readable logical event and invokes only its exact current deserializer after all version checks pass.</summary>
    /// <remarks>The private effective owner is cleared on success, refusal, exception and original-token cancellation.</remarks>
    internal async ValueTask<object> ResolveAndDeserializeAsync(string domain, string eventTypeName,
        int metadataVersion, string? eventContractType, int? payloadVersion, string serializationFormat,
        ReadOnlyMemory<byte> applicationPayload, string aggregateType, CancellationToken cancellationToken,
        EventBufferBudget? sharedBudget = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        Resolver.RequireNoObservedLoss(cancellationToken);
        if (applicationPayload.Length > 64 * 1024 * 1024)
        {
            throw new InvalidOperationException("ReadableLimit: logical source payload exceeds 64 MiB.");
        }

        byte[] before = SHA256.HashData(applicationPayload.Span);
        try
        {
            using ResolvedLogicalEvent resolved = await Resolver.ResolveAsync(domain, eventTypeName,
                metadataVersion, eventContractType, payloadVersion, serializationFormat, applicationPayload,
                cancellationToken, sharedBudget, aggregateType).ConfigureAwait(false);
            object value = _deserializers[resolved.CanonicalType].Deserialize(_registry, resolved.CanonicalType,
                resolved.Payload, cancellationToken);
            Resolver.RequireNoObservedLoss(cancellationToken);
            byte[] after = SHA256.HashData(applicationPayload.Span);
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(before, after))
                {
                    throw new InvalidOperationException("UpcasterContractViolation: original application payload changed during deserialization.");
                }
            }
            finally { CryptographicOperations.ZeroMemory(after); }

            Resolver.RequireNoObservedLoss(cancellationToken);
            return value;
        }
        finally { CryptographicOperations.ZeroMemory(before); }
    }

    private void RequireVersionBindings(string canonicalType, int version, CancellationToken cancellationToken)
    {
        _validations[(canonicalType, version)].RequireDescriptor(_registry, canonicalType, version, cancellationToken);
        _deserializers[canonicalType].RequireDescriptor(_registry, canonicalType, cancellationToken);
    }

    private void ValidateVersion(string domain, string canonicalType, int version, string format,
        Hexalith.EventStore.Contracts.Events.IReadOnlyPayload payload, CancellationToken cancellationToken)
        => _validations[(canonicalType, version)].Validate(domain, canonicalType, version, format, payload, cancellationToken);
}
