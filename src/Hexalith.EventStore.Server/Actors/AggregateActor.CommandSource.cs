using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Streams;

namespace Hexalith.EventStore.Server.Actors;

public partial class AggregateActor
{
    private async Task<CompleteCommandSource> CaptureCompleteCommandSourceAsync(CommandEnvelope command,
        ConditionalValue<AggregateMetadata> stored, IReadOnlyList<EventEnvelope> raw,
        IReadOnlyList<EventEnvelope> readable, ICompleteCommandSourceAuthority authority, CancellationToken cancellationToken)
    {
        var identity = command.AggregateIdentity;
        CompleteCommandSourceAuthorization grant = await authority.AuthorizeAsync(identity, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("CompleteCommandSourceUnavailable");
        DateTimeOffset now = IdempotencyTimeProvider.GetUtcNow();
        if (grant.Identity != identity || string.IsNullOrWhiteSpace(grant.AuthorityRevision) || grant.AuthorityRevision.Length > 256
            || grant.ValidUntil <= now || stored.HasValue && (stored.Value.SourceErasurePending || stored.Value.RetainedFloor != 1)
            || raw.Count != readable.Count || raw.Count > CompleteCommandSource.MaximumEvents
            || (stored.HasValue ? stored.Value.CurrentSequence : 0) != raw.Count)
            throw new InvalidOperationException("CompleteCommandSourceUnavailable");
        SourceProof rebuilt = SourceProofCodec.CreateSeed(identity, stored.HasValue && stored.Value.SourceProof is { } original
            ? original.SourceIncarnation : UniqueIdHelper.GenerateSortableUniqueStringId());
        long bytes = 0;
        var events = new List<StreamReadEvent>();
        for (int index = 0; index < raw.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EventEnvelope native = raw[index]; EventEnvelope plain = readable[index];
            if (native.Identity != identity || native.SequenceNumber != index + 1L || native.MetadataVersion != 1
                || native.EventContractType is not null || native.PayloadVersion is < 1 or > 1
                || plain.SerializationFormat != "json" || plain.Payload is null
                || (bytes += plain.Payload.Length) > CompleteCommandSource.MaximumPayloadBytes)
                throw new InvalidOperationException("CompleteCommandSourceUnavailable");
            using JsonDocument json = JsonDocument.Parse(plain.Payload);
            string digest = SourceProofCodec.ComputeEventHash(native);
            rebuilt = SourceProofCodec.ExtendVerified(rebuilt, native, digest);
            events.Add(new(plain.SequenceNumber, plain.EventTypeName, plain.Payload.ToArray(), plain.SerializationFormat,
                plain.MetadataVersion, plain.MessageId, plain.CorrelationId, plain.CausationId, plain.Timestamp, plain.UserId)
                { NativeEventDigest = digest, GlobalPosition = plain.GlobalPosition, PayloadVersion = plain.PayloadVersion });
        }
        if (stored.HasValue && stored.Value.SourceProof is { } proof && proof != rebuilt)
            throw new InvalidOperationException("CompleteCommandSourceUnavailable");
        // Enrollment authorizes a local complete-prefix bootstrap. The empty stream is durably represented,
        // so retry/restart cannot silently assign a different incarnation to the same captured command.
        AggregateMetadata captured = (stored.HasValue ? stored.Value : new AggregateMetadata(0, now, null)) with { SourceProof = rebuilt };
        if (!stored.HasValue || stored.Value.SourceProof is null)
        {
            await StateManager.SetStateAsync(identity.MetadataKey, captured, cancellationToken).ConfigureAwait(false);
            await StateManager.SaveStateAsync(cancellationToken).ConfigureAwait(false);
        }
        DateTimeOffset expires = grant.ValidUntil < now.AddMinutes(5) ? grant.ValidUntil : now.AddMinutes(5);
        var source = new CompleteCommandSource(identity, grant.AuthorityRevision, now, expires, SourceProofCodec.ToNative(rebuilt), events.AsReadOnly());
        await CompareCompleteCommandSourceAsync(source, authority, cancellationToken).ConfigureAwait(false);
        return source;
    }

    private async Task CompareCompleteCommandSourceAsync(CompleteCommandSource? source,
        ICompleteCommandSourceAuthority? authority, CancellationToken cancellationToken)
    {
        if (source is null) return;
        CompleteCommandSourceAuthorization? grant = authority is null ? null
            : await authority.AuthorizeAsync(source.Identity, cancellationToken).ConfigureAwait(false);
        if (grant is null || grant.Identity != source.Identity || grant.AuthorityRevision != source.AuthorityRevision
            || grant.ValidUntil <= IdempotencyTimeProvider.GetUtcNow() || source.ValidUntil <= IdempotencyTimeProvider.GetUtcNow())
            throw new InvalidOperationException("CompleteCommandSourceChanged");
        await StateManager.ClearCacheAsync(cancellationToken).ConfigureAwait(false);
        var metadata = await StateManager.TryGetStateAsync<AggregateMetadata>(source.Identity.MetadataKey, cancellationToken).ConfigureAwait(false);
        if (!metadata.HasValue || metadata.Value.SourceErasurePending || metadata.Value.RetainedFloor != 1
            || metadata.Value.CurrentSequence != source.Proof.HeadSequence || metadata.Value.SourceProof is not { } proof
            || SourceProofCodec.ToNative(proof) != source.Proof)
            throw new InvalidOperationException("CompleteCommandSourceChanged");
        await ValidateCompleteCommandSourceAuthorityAsync(source, authority, cancellationToken).ConfigureAwait(false);
    }

    // This final commit fence must preserve all staged events, checkpoints, witnesses and protection work.
    private async Task ValidateCompleteCommandSourceAuthorityAsync(CompleteCommandSource? source,
        ICompleteCommandSourceAuthority? authority, CancellationToken cancellationToken)
    {
        if (source is null) return;
        var finalGrant = authority is null ? null : await authority.AuthorizeAsync(source.Identity, cancellationToken).ConfigureAwait(false);
        if (finalGrant is null || finalGrant.Identity != source.Identity || finalGrant.AuthorityRevision != source.AuthorityRevision
            || finalGrant.ValidUntil <= IdempotencyTimeProvider.GetUtcNow() || source.ValidUntil <= IdempotencyTimeProvider.GetUtcNow())
            throw new InvalidOperationException("CompleteCommandSourceChanged");
    }
}
