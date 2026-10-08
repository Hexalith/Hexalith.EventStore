using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Binds an explicit callable to exact implementation identity, file bytes and canonical declared options.</summary>
/// <remarks>A bound runtime source is checked before callback use; sealed dependency graph and startup readiness remain separate requirements.</remarks>
internal sealed class EventImplementationBinding
{
    private readonly string _implementationId;
    private readonly byte[] _assemblyHash;
    private readonly byte[] _optionsHash;
    private readonly EventOptionRule[] _optionSchema;
    private readonly Func<ReadOnlyMemory<byte>>? _runtimeOptions;
    private readonly EventManagedArtifactExecutionBinding? _executionBinding;
    private readonly System.Reflection.Assembly _implementationAssembly;

    /// <summary>Computes direct file and expanded schema hashes before callable use.</summary>
    internal EventImplementationBinding(string implementationId, Delegate implementation, ReadOnlyMemory<byte> canonicalOptions,
        IReadOnlyList<EventOptionRule> optionSchema, Func<ReadOnlyMemory<byte>>? runtimeOptions = null,
        EventManagedArtifactExecutionBinding? executionBinding = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(implementationId);
        ArgumentNullException.ThrowIfNull(implementation);
        ArgumentNullException.ThrowIfNull(optionSchema);
        if (implementation.GetInvocationList().Length != 1)
        {
            throw new ArgumentException("A registered implementation must be one explicit callable.", nameof(implementation));
        }
        _implementationId = implementationId;
        _optionSchema = optionSchema.ToArray();
        _optionsHash = EventOptionsManifestCodec.ComputeHash(canonicalOptions, _optionSchema);
        _runtimeOptions = runtimeOptions;
        _implementationAssembly = implementation.Method.Module.Assembly;
        _executionBinding = executionBinding;
        if (executionBinding is not null)
        {
            _assemblyHash = executionBinding.CopyHashForAssembly(_implementationAssembly);
            return;
        }

        string location = _implementationAssembly.Location;
        if (string.IsNullOrEmpty(location))
        {
            throw new ArgumentException("An implementation file must be resolvable.", nameof(implementation));
        }

        using FileStream stream = File.OpenRead(location);
        _assemblyHash = SHA256.HashData(stream);
    }

    /// <summary>Requires the exact adjacent ID, assembly and options fields from a decoded descriptor.</summary>
    internal void RequireFields(EventRegistryRow descriptor, int implementationField)
    {
        RequireDeclaredFields(descriptor, implementationField);
        if (_runtimeOptions is not null) { RequireRuntimeOptions(); }
        RequireDeclaredFields(descriptor, implementationField);
    }

    /// <summary>Checks immutable descriptor fields without invoking the implementation's runtime options callback.</summary>
    internal void RequireDeclaredFields(EventRegistryRow descriptor, int implementationField)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        _executionBinding?.RequireBoundAssembly(_implementationAssembly);
        if (!string.Equals(_implementationId, descriptor.GetTextField(implementationField), StringComparison.Ordinal)
            || !_assemblyHash.AsSpan().SequenceEqual(descriptor.GetEncodedField(implementationField + 1))
            || !_optionsHash.AsSpan().SequenceEqual(descriptor.GetEncodedField(implementationField + 2)))
        {
            throw new InvalidOperationException("CapabilityMismatch: implementation bytes or options disagree with the descriptor.");
        }
    }

    /// <summary>Checks direct-image evidence shares the executing registry's sticky loss scope.</summary>
    internal void RequireCapabilityScope(EventEvolutionCapabilityLoss capabilityLoss)
        => _executionBinding?.RequireCapabilityScope(capabilityLoss);

    /// <summary>Checks current implementation-owned settings against the expanded declared options.</summary>
    internal void RequireRuntimeOptions()
    {
        _executionBinding?.RequireBoundAssembly(_implementationAssembly);
        if (_runtimeOptions is null)
        {
            throw new InvalidOperationException("CapabilityMismatch: no runtime settings source is bound to this implementation.");
        }

        byte[] currentHash = EventOptionsManifestCodec.ComputeHash(_runtimeOptions(), _optionSchema);
        try
        {
            _executionBinding?.RequireBoundAssembly(_implementationAssembly);
            if (!_optionsHash.AsSpan().SequenceEqual(currentHash))
            {
                throw new InvalidOperationException("CapabilityMismatch: executing runtime settings disagree with the sealed options manifest.");
            }
        }
        finally { CryptographicOperations.ZeroMemory(currentHash); }
    }
}
