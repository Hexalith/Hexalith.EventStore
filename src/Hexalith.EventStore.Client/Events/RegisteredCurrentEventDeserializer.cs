using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Uses an explicitly supplied current CLR type and serializer without CLR name resolution.</summary>
/// <remarks>Only a source-authenticated, schema/identity-validated effective payload may enter this local binding.</remarks>
internal sealed class RegisteredCurrentEventDeserializer
{
    private readonly Type _currentType;
    private readonly Func<IReadOnlyPayload, CancellationToken, object> _deserialize;
    private readonly EventImplementationBinding _serializer;
    private readonly byte[] _typeAssemblyHash;
    private readonly EventManagedArtifactExecutionBinding? _typeExecutionBinding;

    /// <summary>Admits an explicit type and callable with implementation-owned canonical options.</summary>
    internal RegisteredCurrentEventDeserializer(Type currentType, string serializerId,
        Func<IReadOnlyPayload, CancellationToken, object> deserialize, ReadOnlyMemory<byte> canonicalOptions,
        IReadOnlyList<EventOptionRule> optionSchema, Func<ReadOnlyMemory<byte>>? runtimeOptions = null,
        EventManagedArtifactExecutionBinding? serializerExecutionBinding = null,
        EventManagedArtifactExecutionBinding? currentTypeExecutionBinding = null)
    {
        ArgumentNullException.ThrowIfNull(currentType);
        ArgumentNullException.ThrowIfNull(deserialize);
        _currentType = currentType;
        _deserialize = deserialize;
        _serializer = new EventImplementationBinding(serializerId, deserialize, canonicalOptions, optionSchema, runtimeOptions,
            serializerExecutionBinding);
        _typeExecutionBinding = currentTypeExecutionBinding;
        if (currentTypeExecutionBinding is not null)
        {
            _typeAssemblyHash = currentTypeExecutionBinding.CopyHashForAssembly(currentType.Assembly);
            return;
        }

        if (string.IsNullOrEmpty(currentType.Assembly.Location))
        {
            throw new ArgumentException("A current type assembly file must be resolvable.", nameof(currentType));
        }

        using FileStream stream = File.OpenRead(currentType.Assembly.Location);
        _typeAssemblyHash = SHA256.HashData(stream);
    }

    /// <summary>Checks D/V bindings before invoking the serializer and requires the exact allow-listed output type.</summary>
    internal object Deserialize(EventDomainRegistry registry, string canonicalType, ImmutablePayload effectivePayload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(effectivePayload);
        cancellationToken.ThrowIfCancellationRequested();
        registry.CapabilityLoss.RequireNoObservedLoss();
        _serializer.RequireCapabilityScope(registry.CapabilityLoss);
        _typeExecutionBinding?.RequireCapabilityScope(registry.CapabilityLoss);
        _typeExecutionBinding?.RequireBoundAssembly(_currentType.Assembly);
        EventRegistryRow current = registry.Rows.Single(row => row.Tag == 0x44
            && string.Equals(row.GetTextKey(1), canonicalType, StringComparison.Ordinal));
        if (!string.Equals(current.GetTextField(3), _currentType.AssemblyQualifiedName, StringComparison.Ordinal)
            || !_typeAssemblyHash.AsSpan().SequenceEqual(current.GetEncodedField(4)))
        {
            throw new InvalidOperationException("CapabilityMismatch: current CLR type is not the explicitly allow-listed D binding.");
        }

        _serializer.RequireFields(registry.GetVersion(canonicalType, current.GetIntField(2)), 4);
        cancellationToken.ThrowIfCancellationRequested();
        registry.CapabilityLoss.RequireNoObservedLoss();
        using var lease = new InvocationPayloadLease(effectivePayload, cancellationToken);
        object value = _deserialize(lease, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        registry.CapabilityLoss.RequireNoObservedLoss();
        if (value is null || value.GetType() != _currentType)
        {
            throw new InvalidOperationException("UpcasterContractViolation: serializer returned a different current CLR type.");
        }

        return value;
    }
}
