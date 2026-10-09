using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Binds version schema and identity callbacks separately to the exact V descriptor fields.</summary>
/// <remarks>Runtime setting equality and dependency/manifest attestation remain separate startup requirements.</remarks>
internal sealed class RegisteredEventVersionValidation
{
    private readonly EventDomainRegistry _registry;
    private readonly EventVersionValidator _schema;
    private readonly EventVersionValidator _identity;
    private readonly EventImplementationBinding _schemaBinding;
    private readonly EventImplementationBinding _identityBinding;

    /// <summary>Admits the two explicit callable implementations and their implementation-owned option schemas.</summary>
    internal RegisteredEventVersionValidation(EventDomainRegistry registry,
        string schemaId, EventVersionValidator schema, ReadOnlyMemory<byte> schemaOptions, IReadOnlyList<EventOptionRule> schemaRules,
        string identityId, EventVersionValidator identity, ReadOnlyMemory<byte> identityOptions, IReadOnlyList<EventOptionRule> identityRules,
        Func<ReadOnlyMemory<byte>>? schemaRuntimeOptions = null, Func<ReadOnlyMemory<byte>>? identityRuntimeOptions = null,
        EventManagedArtifactExecutionBinding? schemaExecutionBinding = null,
        EventManagedArtifactExecutionBinding? identityExecutionBinding = null,
        EventManagedArtifactExecutionBinding? schemaRuntimeOptionsExecutionBinding = null,
        EventManagedArtifactExecutionBinding? identityRuntimeOptionsExecutionBinding = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(identity);
        _registry = registry;
        _schema = schema;
        _identity = identity;
        _schemaBinding = new EventImplementationBinding(schemaId, schema, schemaOptions, schemaRules, schemaRuntimeOptions,
            schemaExecutionBinding, schemaRuntimeOptionsExecutionBinding);
        _identityBinding = new EventImplementationBinding(identityId, identity, identityOptions, identityRules, identityRuntimeOptions,
            identityExecutionBinding, identityRuntimeOptionsExecutionBinding);
        _schemaBinding.RequireCapabilityScope(registry.CapabilityLoss);
        _identityBinding.RequireCapabilityScope(registry.CapabilityLoss);
    }

    /// <summary>Checks exact registry ownership and both immutable V bindings without invoking application callbacks.</summary>
    internal void RequireDescriptor(EventDomainRegistry registry, string canonicalType, int version,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        registry.CapabilityLoss.RequireNoObservedLoss();
        registry.RequireActive(cancellationToken);
        if (!ReferenceEquals(registry, _registry))
        {
            throw new InvalidOperationException("CapabilityMismatch: version validation belongs to another registry.");
        }

        EventRegistryRow descriptor = registry.GetVersion(canonicalType, version);
        _schemaBinding.RequireDeclaredFields(descriptor, 1);
        _identityBinding.RequireDeclaredFields(descriptor, 8);
        cancellationToken.ThrowIfCancellationRequested();
        registry.CapabilityLoss.RequireNoObservedLoss();
    }

    /// <summary>Checks both bindings before invoking either callback with independently expired immutable facades.</summary>
    internal void Validate(string domain, string canonicalType, int version, string format, IReadOnlyPayload payload,
        CancellationToken cancellationToken)
    {
        RequireValidation(domain, canonicalType, version, format, payload, cancellationToken);
        using (var schemaLease = new InvocationPayloadLease(payload, cancellationToken))
        {
            _schema(domain, canonicalType, version, format, schemaLease, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _registry.CapabilityLoss.RequireNoObservedLoss();
        }
        using (var identityLease = new InvocationPayloadLease(payload, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _registry.CapabilityLoss.RequireNoObservedLoss();
            _identity(domain, canonicalType, version, format, identityLease, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _registry.CapabilityLoss.RequireNoObservedLoss();
        }
    }

    /// <summary>Rechecks addressed source/trust/binding between the separately leased current schema and identity callbacks.</summary>
    internal async ValueTask ValidateAsync(string domain, string canonicalType, int version, string format, IReadOnlyPayload payload,
        Func<CancellationToken, Task>? sourceFence, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Task FenceAsync(CancellationToken cancellation) => EventCallbackFence.RequireAsync(_registry, sourceFence, cancellation,
            () => RequireValidationDescriptor(domain, canonicalType, version, format, cancellation)).AsTask();
        await FenceAsync(token).ConfigureAwait(false);
        EventRegistryRow descriptor = _registry.GetVersion(canonicalType, version);
        await _schemaBinding.RequireFieldsAsync(descriptor, 1, FenceAsync, token,
            () => RequireValidationDescriptor(domain, canonicalType, version, format, token)).ConfigureAwait(false);
        await FenceAsync(token).ConfigureAwait(false);
        using (var schemaLease = new InvocationPayloadLease(payload, token))
        {
            try { _schema(domain, canonicalType, version, format, schemaLease, token); }
            finally { token.ThrowIfCancellationRequested(); }
        }
        await FenceAsync(token).ConfigureAwait(false);
        await _identityBinding.RequireFieldsAsync(descriptor, 8, FenceAsync, token,
            () => RequireValidationDescriptor(domain, canonicalType, version, format, token)).ConfigureAwait(false);
        await FenceAsync(token).ConfigureAwait(false);
        using (var identityLease = new InvocationPayloadLease(payload, token))
        {
            try { _identity(domain, canonicalType, version, format, identityLease, token); }
            finally { token.ThrowIfCancellationRequested(); }
        }
        await FenceAsync(token).ConfigureAwait(false);
    }

    private void RequireValidation(string domain, string canonicalType, int version, string format, IReadOnlyPayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        RequireValidationDescriptor(domain, canonicalType, version, format, cancellationToken);
        EventRegistryRow descriptor = _registry.GetVersion(canonicalType, version);
        _schemaBinding.RequireFields(descriptor, 1, cancellationToken);
        RequireValidationDescriptor(domain, canonicalType, version, format, cancellationToken);
        _identityBinding.RequireFields(descriptor, 8, cancellationToken);
        RequireValidationDescriptor(domain, canonicalType, version, format, cancellationToken);
    }

    private void RequireValidationDescriptor(string domain, string canonicalType, int version, string format,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _registry.RequireActive(cancellationToken);
        RequireDescriptor(_registry, canonicalType, version, cancellationToken);
        EventRegistryRow descriptor = _registry.GetVersion(canonicalType, version);
        if (!string.Equals(domain, _registry.Domain, StringComparison.Ordinal)
            || !string.Equals(format, descriptor.GetTextField(7), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("CapabilityMismatch: validation scope or format disagrees with the registered V descriptor.");
        }
    }
}
