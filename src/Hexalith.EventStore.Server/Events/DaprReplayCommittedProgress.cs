namespace Hexalith.EventStore.Server.Events;

/// <summary>Carries private committed progress produced only by the operation owner's fresh participant readback.</summary>
internal sealed class DaprReplayCommittedProgress
{
    /// <summary>Snapshots the operation-owned source, registry, sequence and accumulator after readback.</summary>
    internal DaprReplayCommittedProgress(byte[] sourceBindingHash, byte[] registryFingerprint, long completedSequence, byte[] accumulator) { SourceBindingHash = sourceBindingHash.ToArray(); RegistryFingerprint = registryFingerprint.ToArray(); CompletedSequence = completedSequence; Accumulator = accumulator.ToArray(); }

    /// <summary>Gets the fixed operation source binding.</summary>
    internal byte[] SourceBindingHash { get; }
    /// <summary>Gets the active registry pinned by the committed operation.</summary>
    internal byte[] RegistryFingerprint { get; }
    /// <summary>Gets the inclusive last committed sequence.</summary>
    internal long CompletedSequence { get; }
    /// <summary>Gets the admitted ledger accumulator.</summary>
    internal byte[] Accumulator { get; }
}
