using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Binds one allow-listed callable to its exact implementation file and configured options digest.</summary>
/// <remarks>The caller must separately attest expanded options and the sealed transitive dependency closure.</remarks>
internal sealed class RegisteredEventUpcaster
{
    private readonly byte[] _assemblyHash;
    private readonly byte[] _optionsHash;
    private readonly EventManagedArtifactExecutionBinding? _executionBinding;

    /// <summary>Hashes the actual directly executing assembly and copies the admitted 32-byte options digest.</summary>
    internal RegisteredEventUpcaster(string implementationId, IEventUpcaster upcaster, byte[] optionsHash,
        EventManagedArtifactExecutionBinding? executionBinding = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(implementationId);
        ArgumentNullException.ThrowIfNull(upcaster);
        ArgumentNullException.ThrowIfNull(optionsHash);
        if (optionsHash.Length != 32)
        {
            throw new ArgumentException("An upcaster options digest must contain exactly 32 bytes.", nameof(optionsHash));
        }

        ImplementationId = implementationId;
        Upcaster = upcaster;
        _optionsHash = optionsHash.ToArray();
        _executionBinding = executionBinding;
        if (executionBinding is not null)
        {
            executionBinding.RequireBoundAssembly(upcaster.GetType().GetInterfaceMap(typeof(IEventUpcaster)).TargetMethods.Single().Module.Assembly);
            _assemblyHash = executionBinding.CopyHashForAssembly(upcaster.GetType().Assembly);
            return;
        }

        string location = upcaster.GetType().Assembly.Location;
        if (string.IsNullOrEmpty(location))
        {
            throw new ArgumentException("An unresolvable implementation assembly cannot be registered.", nameof(upcaster));
        }

        using FileStream stream = File.OpenRead(location);
        _assemblyHash = SHA256.HashData(stream);
    }

    /// <summary>Gets the exact registered implementation identity.</summary>
    internal string ImplementationId { get; }

    /// <summary>Gets the allow-listed callable; it receives only invocation-scoped facades.</summary>
    internal IEventUpcaster Upcaster { get; }

    /// <summary>Checks the callable's direct assembly and options against the exact E descriptor.</summary>
    internal void RequireDescriptor(EventRegistryRow edge, EventEvolutionCapabilityLoss capabilityLoss)
    {
        _executionBinding?.RequireCapabilityScope(capabilityLoss);
        _executionBinding?.RequireBoundAssembly(Upcaster.GetType().Assembly);
        if (!string.Equals(ImplementationId, edge.GetTextField(7), StringComparison.Ordinal)
            || !_assemblyHash.AsSpan().SequenceEqual(edge.GetEncodedField(8))
            || !_optionsHash.AsSpan().SequenceEqual(edge.GetEncodedField(9)))
        {
            throw new InvalidOperationException("CapabilityMismatch: upcaster implementation or options disagree with the registered edge.");
        }
    }
}
