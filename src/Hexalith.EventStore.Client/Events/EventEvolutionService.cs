using System.Collections.Frozen;
using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Events;

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
        registry.RequireActive(CancellationToken.None);
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
        registry.RequireActive(CancellationToken.None);
        registry.CapabilityLoss.RequireNoObservedLoss();
    }

    /// <summary>Gets the composed resolver for addressed Dapr page preparation and bounded effective resolution.</summary>
    internal EventLogicalViewResolver Resolver { get; }

    /// <summary>Gets the exact immutable registry identity shared by all composed evolution routes.</summary>
    internal string RegistryFingerprint
    {
        get {
            _registry.RequireActive(CancellationToken.None);
            return _registry.Fingerprint;
        }
    }

    /// <summary>Gets the supplied event registry domain.</summary>
    internal string Domain => _registry.Domain;

    /// <summary>Admits an explicitly declared aggregate route without invoking event callbacks.</summary>
    internal void RequireAggregateRoute(string domain, string aggregateType, CancellationToken token)
    {
        RequireActive(token);
        if (domain != _registry.Domain || !_registry.Rows.Where(static row => row.Tag == 0x44)
            .Any(row => _registry.GetAggregateRoute(row.GetTextKey(1)) == aggregateType))
        {
            throw new InvalidOperationException("CapabilityMismatch: aggregate route is not declared in the supplied event registry.");
        }
    }

    /// <summary>Gets the shared observed-loss scope required by the logical source and trust composition.</summary>
    internal EventEvolutionCapabilityLoss CapabilityLoss
    {
        get {
            _registry.RequireActive(CancellationToken.None);
            return _registry.CapabilityLoss;
        }
    }

    /// <summary>Checks the supplied registry instance and its shared capability before addressed source or callback admission.</summary>
    internal void RequireActive(CancellationToken cancellationToken)
    {
        _registry.RequireActive(cancellationToken);
        Resolver.RequireNoObservedLoss(cancellationToken);
    }

    /// <summary>Admits an effective route without invoking application code.</summary>
    internal void RequireCurrentRoute(string domain, string aggregateType, string canonicalType, int version,
        string format, CancellationToken cancellationToken)
    {
        RequireActive(cancellationToken);
        if (version != _registry.GetCurrentVersion(canonicalType))
        {
            throw new InvalidOperationException("CapabilityMismatch: logical replay requires the current event version.");
        }
        Resolver.RequireReadableSource(domain, canonicalType, 2, canonicalType, version, format, aggregateType, cancellationToken);
    }

    /// <summary>Deserializes a privately admitted signed current view through the shared catalog.</summary>
    internal async ValueTask<object> DeserializeCurrentAsync(string domain, string aggregateType, string canonicalType, int version,
        string format, IReadOnlyPayload payload, Func<CancellationToken, Task> sourceFence, CancellationToken cancellationToken)
    {
        RequireCurrentRoute(domain, aggregateType, canonicalType, version, format, cancellationToken);
        await _validations[(canonicalType, version)].ValidateAsync(domain, canonicalType, version, format, payload,
            sourceFence, cancellationToken).ConfigureAwait(false);
        await sourceFence(cancellationToken).ConfigureAwait(false);
        RequireActive(cancellationToken);
        object value = _deserializers[canonicalType].Deserialize(_registry, canonicalType, payload, cancellationToken);
        await sourceFence(cancellationToken).ConfigureAwait(false);
        RequireActive(cancellationToken);
        return value;
    }

    /// <summary>Resolves a readable logical event and invokes only its exact current deserializer after all version checks pass.</summary>
    /// <remarks>The private effective owner is cleared on success, refusal, exception and original-token cancellation.</remarks>
    internal async ValueTask<object> ResolveAndDeserializeAsync(string domain, string eventTypeName,
        int metadataVersion, string? eventContractType, int? payloadVersion, string serializationFormat,
        ReadOnlyMemory<byte> applicationPayload, string aggregateType, CancellationToken cancellationToken,
        EventBufferBudget? sharedBudget = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        RequireActive(cancellationToken);
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
            RequireActive(cancellationToken);
            byte[] after = SHA256.HashData(applicationPayload.Span);
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(before, after))
                {
                    throw new InvalidOperationException("UpcasterContractViolation: original application payload changed during deserialization.");
                }
            }
            finally { CryptographicOperations.ZeroMemory(after); }

            RequireActive(cancellationToken);
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
