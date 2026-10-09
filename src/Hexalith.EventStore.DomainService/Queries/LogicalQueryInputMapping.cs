using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.DomainService.Queries;
/// <summary>Maps one exact origin type to a registered CLR materializer and bounded graph.</summary>
internal sealed class LogicalQueryInputMapping
{
    private readonly Action<IReadOnlyPayload, CancellationToken> _validate;
    private readonly Func<IReadOnlyPayload, CancellationToken, object> _read;
    private readonly LogicalQueryCallable _validator;
    private readonly LogicalQueryCallable _materializer;
    /// <summary>Freezes the explicit mapping and both local immutable callable declarations.</summary>
    internal LogicalQueryInputMapping(string id, string valueTypeName, Type valueType, int maximumGraphBytes, Action<IReadOnlyPayload, CancellationToken> validate, Func<IReadOnlyPayload, CancellationToken, object> read, LogicalQueryCallable validator, LogicalQueryCallable materializer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueTypeName);
        ArgumentNullException.ThrowIfNull(valueType);
        if (maximumGraphBytes is < 1 or > 64 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumGraphBytes));
        }

        Id = id;
        ValueTypeName = valueTypeName;
        ValueType = valueType;
        MaximumGraphBytes = maximumGraphBytes;
        _validate = validate;
        _read = read;
        _validator = validator;
        _materializer = materializer;
        if (!ReferenceEquals(validator.CapabilityLoss, materializer.CapabilityLoss))
        {
            throw new InvalidOperationException("QueryCapabilityChanged: input callbacks have foreign observed-loss scopes.");
        }

        _validator.RequireCallable(validate);
        _materializer.RequireCallable(read);
    }

    /// <summary>Gets the exact immutable shared observed-loss scope.</summary>
    internal EventEvolutionCapabilityLoss CapabilityLoss => _validator.CapabilityLoss;
    /// <summary>Gets the frozen mapping identity.</summary>
    internal string Id { get; }
    /// <summary>Gets the exact originating logical-operation value type.</summary>
    internal string ValueTypeName { get; }
    /// <summary>Gets the exact admitted internal CLR type.</summary>
    internal Type ValueType { get; }
    /// <summary>Gets the conservative graph reservation retained until scope closure.</summary>
    internal int MaximumGraphBytes { get; }

    /// <summary>Encodes the exact CLR/origin/schema/graph mapping bound by query and registration options.</summary>
    internal byte[] EncodeManifest()
    {
        using FileStream file = File.OpenRead(ValueType.Assembly.Location);
        byte[] typeHash = System.Security.Cryptography.SHA256.HashData(file);
        byte[] validatorSchema = _validator.EncodeSchema(), materializerSchema = _materializer.EncodeSchema();
        using var writer = new EventEvolutionBinaryWriter(checked(4096 + validatorSchema.Length + materializerSchema.Length));
        try
        {
            writer.WriteString(Id);
            writer.WriteString(ValueTypeName);
            writer.WriteString(ValueType.AssemblyQualifiedName!);
            writer.WriteHash(typeHash);
            writer.WriteInt32(MaximumGraphBytes);
            writer.WriteBytes(validatorSchema);
            writer.WriteBytes(materializerSchema);
            return writer.CopyEncodedBytes();
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(typeHash);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(validatorSchema);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(materializerSchema);
        }
    }

    /// <summary>Validates then materializes with expired payload leases around each actual owner await.</summary>
    internal async Task<object> ReadAsync(ImmutablePayload payload, Func<CancellationToken, Task> fence, EventBufferBudget budget, CancellationToken token)
    {
        await _validator.RequireAsync(fence, budget, token).ConfigureAwait(false);
        using (var lease = new InvocationPayloadLease(payload, token))
        {
            try
            {
                _validate(lease, token);
            }
            finally
            {
                token.ThrowIfCancellationRequested();
            }
        }

        await fence(token).ConfigureAwait(false);
        await _materializer.RequireAsync(fence, budget, token).ConfigureAwait(false);
        object value;
        using (var lease = new InvocationPayloadLease(payload, token))
        {
            try
            {
                value = _read(lease, token);
            }
            finally
            {
                token.ThrowIfCancellationRequested();
            }
        }

        await fence(token).ConfigureAwait(false);
        if (value is null || value.GetType() != ValueType)
        {
            throw new InvalidOperationException("ReadModelQueryConsistencyHold: materializer returned an undeclared type.");
        }

        return value;
    }
}
