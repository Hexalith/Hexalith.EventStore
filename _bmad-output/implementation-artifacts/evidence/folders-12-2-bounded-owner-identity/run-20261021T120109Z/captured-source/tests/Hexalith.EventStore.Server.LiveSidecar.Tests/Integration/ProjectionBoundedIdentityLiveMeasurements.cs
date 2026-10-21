using System.Text.Json;
using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Records observed physical writes and canonical batches from the production protocol.</summary>
internal sealed class ProjectionBoundedIdentityLiveMeasurements
{
    public int MaximumPhysicalValueBytes { get; private set; }
    public int MaximumIdentityValueBytes { get; private set; }
    public int MaximumCheckpointValueBytes { get; private set; }
    public int MaximumOwnerManifestValueBytes { get; private set; }
    public int MaximumCanonicalBatchBytes { get; private set; }
    public int MaximumBatchOperations { get; private set; }
    public long PhysicalWrites { get; private set; }

    internal void ObserveValue(string key, int bytes)
    {
        PhysicalWrites++;
        MaximumPhysicalValueBytes = Math.Max(MaximumPhysicalValueBytes, bytes);
        if (key.StartsWith("shared-identity-v1:", StringComparison.Ordinal)) MaximumIdentityValueBytes = Math.Max(MaximumIdentityValueBytes, bytes);
        if (key.StartsWith("shared-checkpoint:", StringComparison.Ordinal)) MaximumCheckpointValueBytes = Math.Max(MaximumCheckpointValueBytes, bytes);
        if (key.Contains(":partition:", StringComparison.Ordinal) && key.EndsWith(":manifest", StringComparison.Ordinal)) MaximumOwnerManifestValueBytes = Math.Max(MaximumOwnerManifestValueBytes, bytes);
    }

    internal void ObserveBatch(ReadModelBatch batch)
    {
        MaximumCanonicalBatchBytes = Math.Max(MaximumCanonicalBatchBytes, ReadModelBatchFingerprint.BuildCanonicalManifest(batch).Length);
        MaximumBatchOperations = Math.Max(MaximumBatchOperations, batch.Operations.Count);
    }
}
