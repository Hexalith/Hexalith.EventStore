using System.Security.Cryptography;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Bounds observed earlier-head hints without granting historical source authority.</summary>
internal static class DaprLogicalSnapshotRewitnessPolicy
{
    /// <summary>Gets the distinct explicit current-prefix policy; historical rebase framing is not accepted.</summary>
    internal const string ModelId = "dapr-actor-logical-current-prefix-rewitness-v1";
    /// <summary>Requires one retained complete current prefix and the same immutable address/configuration under a declared older head.</summary>
    internal static void RequireBindings(string model, DaprLogicalSourceBinding prior, DaprLogicalSourceBinding current)
    {
        ArgumentNullException.ThrowIfNull(prior);
        ArgumentNullException.ThrowIfNull(current);
        if (model != ModelId || current.TargetSequence < 1 || prior.TargetSequence != current.TargetSequence || prior.ActorHead < prior.TargetSequence || prior.ActorHead >= current.ActorHead || current.TargetSequence > current.ActorHead || prior.RetainedFloor != 1 || current.RetainedFloor != 1 || !prior.MetadataPresent || !current.MetadataPresent || prior.ApplicationId != current.ApplicationId || prior.Namespace != current.Namespace || prior.ActorType != current.ActorType || prior.Identity != current.Identity || prior.AggregateType != current.AggregateType || !prior.SourceConfigurationHash.Span.SequenceEqual(current.SourceConfigurationHash.Span))
        {
            throw new InvalidOperationException("SnapshotRewitnessHold: exact current complete prefix and same-scope earlier-head hint are required; historical rebase remains unavailable.");
        }
    }

    /// <summary>Checks strictly captured prior fields and exact state bytes, without authenticating the claimed historical operation.</summary>
    internal static void RequirePrior(DaprLogicalSnapshotWrite write, DaprLogicalSourceBinding prior, DaprLogicalClaimTrust trust, RegisteredLogicalReplayBinding reconstruction)
    {
        write.RequirePriorPins();
        using EventBufferReservation decoding = write.Budget.Reserve(checked(4 * write.PriorWitness.Bytes.Length + 4096));
        DaprLogicalSnapshotWitness fields;
        try
        {
            fields = DaprLogicalSnapshotCodec.DecodeSnapshot(write.PriorWitness.Bytes);
        }
        catch (Exception exception)when (exception is ArgumentException or InvalidOperationException or System.IO.InvalidDataException)
        {
            throw new InvalidOperationException("SnapshotRewitnessHold: observed prior witness is malformed.", exception);
        }

        if (fields.TenantId != prior.Identity.TenantId || fields.Domain != prior.Identity.Domain || fields.AggregateId != prior.Identity.AggregateId || fields.AggregateType != prior.AggregateType || fields.CoveredSequence != prior.TargetSequence || fields.StorageKey != write.StorageKey || fields.WitnessKey != write.WitnessKey || fields.SerializerId != reconstruction.SerializerId || !fields.SourceBindingHash.Span.SequenceEqual(DaprLogicalClaimCodec.ComputeSourceBindingHash(prior, write.Budget)) || !fields.RegistryFingerprint.Span.SequenceEqual(trust.RegistryFingerprint.Span) || !fields.ReconstructionBindingHash.Span.SequenceEqual(reconstruction.Fingerprint.Span) || !fields.StorageHash.Span.SequenceEqual(SHA256.HashData(write.PriorState.Bytes.Span)) || !fields.FoldedHash.Span.SequenceEqual(fields.StorageHash.Span))
        {
            throw new InvalidOperationException("SnapshotRewitnessHold: observed prior pair differs from its bounded hint or current codec.");
        }
    }
}
