using System.Reflection;
using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Binds one exact Assembly object to the private image supplied by its retained artifact owner.</summary>
/// <remarks>This direct-image evidence grants no transitive, native, catalog or serving-peer readiness.</remarks>
internal sealed class EventManagedArtifactExecutionBinding : IDisposable
{
    private readonly object _gate = new();
    private readonly EventManagedArtifact _owner;
    private readonly byte[] _hash;
    private bool _disposed;

    /// <summary>Captures the direct-image digest after loading from the owner's privately admitted bytes.</summary>
    internal EventManagedArtifactExecutionBinding(EventManagedArtifact owner, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(assembly);
        _owner = owner;
        Assembly = assembly;
        _hash = owner.CopyLoadedHashForAssembly(assembly);
    }

    /// <summary>Gets the loaded object whose origin is bound to the retained private image.</summary>
    internal Assembly Assembly { get; }

    /// <summary>Requires the callback's declaring assembly to be this exact admitted runtime object.</summary>
    internal void RequireBoundAssembly(Assembly assembly)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _owner.RequireActive();
            if (!ReferenceEquals(assembly, Assembly))
            {
                throw new InvalidOperationException("CapabilityMismatch: the callback assembly is not the admitted managed image object.");
            }
        }
    }

    /// <summary>Copies a direct-image hash only for the exact still-admitted callback assembly.</summary>
    internal byte[] CopyHashForAssembly(Assembly assembly)
    {
        lock (_gate)
        {
            RequireBoundAssembly(assembly);
            return _hash.ToArray();
        }
    }

    /// <summary>Requires the executing registry to observe the same capability loss as this artifact.</summary>
    internal void RequireCapabilityScope(EventEvolutionCapabilityLoss capabilityLoss)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _owner.RequireCapabilityScope(capabilityLoss);
        }
    }

    /// <summary>Requires observation to use the exact dependency row originally admitted by the artifact owner.</summary>
    internal void RequireDependencyDeclaration(EventRegistryRow declaration)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _owner.RequireDependencyDeclaration(declaration);
        }
    }

    /// <summary>Ends binding evidence and fences subsequent capability before clearing its digest.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) { return; }
            _disposed = true;
            _owner.ObserveBindingLoss();
            CryptographicOperations.ZeroMemory(_hash);
        }
    }
}
