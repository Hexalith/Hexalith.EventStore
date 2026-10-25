using System.Security.Claims;
using System.Text.Json;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.DomainService;

/// <summary>Verifies an exact authenticated complete native request locally, without source reads or actor re-entry.</summary>
public sealed class CompleteCommandSourceVerifier(TimeProvider clock)
{
    /// <summary>Returns the captured complete source or denies malformed, incomplete, stale or unbound input.</summary>
    public CompleteCommandSource Verify(DomainServiceRequest request, ClaimsPrincipal workload, string platformWorkloadId)
    {
        ArgumentNullException.ThrowIfNull(request);
        CompleteCommandSource? supplied = request.CompleteSource;
        DateTimeOffset now = clock.GetUtcNow();
        if (supplied is null || supplied.Identity != request.Command.AggregateIdentity || supplied.Proof is null
            || supplied.Events is null || supplied.Events.Count > CompleteCommandSource.MaximumEvents
            || string.IsNullOrWhiteSpace(supplied.AuthorityRevision) || supplied.AuthorityRevision.Length > 256
            || supplied.ObservedAt == default || supplied.ObservedAt > now || supplied.ValidUntil <= now
            || supplied.ValidUntil <= supplied.ObservedAt || supplied.ValidUntil - supplied.ObservedAt > TimeSpan.FromMinutes(5)
            || request.CommandStateProof is not null || request.VerifiedEffectiveEvents is not null
            || request.WriterMode is not null || request.RegistryFingerprint is not null
            || request.IncludePostCommandState || request.SnapshotCaptureProfileId is not null
            || supplied.Proof.HeadSequence != supplied.Events.Count || supplied.Proof.RetainedFloor != 1 || supplied.Proof.Erased
            || !NativeSourceProofCodec.IsValid(supplied.Proof, supplied.Identity, supplied.Events.Count, 1)
            || !CommandSourceAuthorityBinding.Verify(request, workload, platformWorkloadId))
            throw new UnauthorizedAccessException("CompleteCommandSourceDenied");

        NativeSourceProof rebuilt = NativeSourceProofCodec.CreateSeed(supplied.Identity, supplied.Proof.SourceIncarnation);
        long bytes = 0;
        var events = new List<StreamReadEvent>();
        foreach (StreamReadEvent item in supplied.Events)
        {
            if (item is null || item.Payload is null || item.SequenceNumber != events.Count + 1L || item.MetadataVersion != 1
                || item.PayloadVersion is < 1 or > 1 || item.SerializationFormat != "json"
                || string.IsNullOrWhiteSpace(item.EventTypeName) || string.IsNullOrWhiteSpace(item.MessageId)
                || item.NativeEventDigest is not { Length: 64 } || (bytes += item.Payload.Length) > CompleteCommandSource.MaximumPayloadBytes)
                throw new UnauthorizedAccessException("CompleteCommandSourceDenied");
            using JsonDocument json = JsonDocument.Parse(item.Payload);
            var owned = item with { Payload = item.Payload.ToArray() };
            rebuilt = NativeSourceProofCodec.ExtendDigest(rebuilt, supplied.Identity, item.SequenceNumber, item.MessageId, item.NativeEventDigest);
            events.Add(owned);
        }
        if (rebuilt != supplied.Proof || !MatchesState(request.CurrentState, events))
            throw new UnauthorizedAccessException("CompleteCommandSourceDenied");
        return supplied with { Events = events.AsReadOnly() };
    }

    private static bool MatchesState(object? current, IReadOnlyList<StreamReadEvent> events)
    {
        DomainServiceCurrentState? state = current switch
        {
            DomainServiceCurrentState typed => typed,
            JsonElement json => json.Deserialize<DomainServiceCurrentState>(JsonSerializerOptions.Web),
            _ => null,
        };
        if (state is null || state.UsedSnapshot || state.LastSnapshotSequence != 0 || state.CurrentSequence != events.Count
            || state.Events is null || state.Events.Count != events.Count) return false;
        for (int index = 0; index < events.Count; index++)
        {
            var expected = events[index]; var actual = state.Events[index];
            if (actual is null || actual.Metadata is not { } metadata || actual.Payload is null || !actual.Payload.AsSpan().SequenceEqual(expected.Payload)
                || metadata.SequenceNumber != expected.SequenceNumber || metadata.MessageId != expected.MessageId
                || metadata.EventTypeName != expected.EventTypeName || metadata.MetadataVersion != expected.MetadataVersion
                || metadata.SerializationFormat != expected.SerializationFormat) return false;
        }
        return true;
    }
}
