using System.Security.Cryptography;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>Binds one supplied trusted replay callable to exact managed bytes and its shared loss scope.</summary>
/// <remarks>File-only bindings qualify local preparation; complete immutable catalogs remain separate gates.</remarks>
internal sealed class LogicalReplayCallableBinding
{
    private readonly System.Reflection.Assembly _assembly;
    private readonly EventManagedArtifactExecutionBinding? _execution;
    private readonly EventEvolutionCapabilityLoss _loss;
    private readonly byte[] _hash;

    /// <summary>Rejects multicast callbacks and admits the exact supplied image.</summary>
    internal LogicalReplayCallableBinding(Delegate callback, EventEvolutionCapabilityLoss loss,
        EventManagedArtifactExecutionBinding? execution)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (callback.GetInvocationList().Length != 1)
        {
            throw new ArgumentException("Replay requires one exact callable.");
        }

        _assembly = callback.Method.Module.Assembly;
        _execution = execution;
        _loss = loss;
        MethodIdentity = callback.Method.DeclaringType?.AssemblyQualifiedName + ":" + callback.Method.MetadataToken;
        if (execution is not null)
        {
            execution.RequireCapabilityScope(loss);
            _hash = execution.CopyHashForAssembly(_assembly);
        }
        else
        {
            using FileStream stream = File.OpenRead(_assembly.Location);
            _hash = SHA256.HashData(stream);
        }

        RequireCurrent(CancellationToken.None);
    }

    /// <summary>Gets the exact callable method identity for the operation pin.</summary>
    internal string MethodIdentity
    {
        get;
    }

    /// <summary>Gets the supplied managed artifact hash.</summary>
    internal ReadOnlySpan<byte> Hash => _hash;

    /// <summary>Checks current artifact evidence, cancellation and observed loss before and after callbacks.</summary>
    internal void RequireCurrent(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _loss.RequireNoObservedLoss();
        if (_execution is not null)
        {
            _execution.RequireCapabilityScope(_loss);
            _execution.RequireBoundAssembly(_assembly);
        }
        else
        {
            using FileStream stream = File.OpenRead(_assembly.Location);
            if (!SHA256.HashData(stream).AsSpan().SequenceEqual(_hash))
            {
                throw new InvalidOperationException("CapabilityMismatch: supplied replay artifact changed.");
            }
        }

        token.ThrowIfCancellationRequested();
        _loss.RequireNoObservedLoss();
    }
}
