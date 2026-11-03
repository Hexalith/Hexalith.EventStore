using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

namespace Hexalith.EventStore.Testing;

/// <summary>Checks read-only seed delivery observations using the actual SDK receipt and fingerprint contracts; supplies no runtime authority.</summary>
public static class ProjectionDeliveryCompletionObserver
{
    /// <summary>Gets the SDK checkpoint key for one exact source and enrolled projection.</summary>
    public static string CheckpointKey(string tenant, string domain, string aggregate, string projection)
    {
        ProjectionKeySegments.Validate(projection, nameof(projection));
        return ProjectionDeliveryStateKeys.GetStateKey(new(tenant, domain, aggregate), projection);
    }

    /// <summary>Gets the actual SDK lifecycle actor identity for one exact source and projection.</summary>
    public static string LifecycleActorId(string tenant, string domain, string aggregate, string projection)
    {
        var identity = new AggregateIdentity(tenant, domain, aggregate);
        ProjectionKeySegments.Validate(projection, nameof(projection));
        return identity.ActorId + ":" + projection;
    }

    /// <summary>Requires an exact complete readable source prefix, SDK completion receipts, and explicit absent lifecycle state between unchanged positive epochs.</summary>
    /// <remarks>Supports the unprotected metadata-v1 readable source layout and both SDK receipt fingerprint versions.
    /// The caller observes actual storage absence and byte/version stability. Missing checkpoint or epoch bytes never establish completion.</remarks>
    public static bool IsComplete(StreamReadPage source, string projection, ReadOnlyMemory<byte> checkpoint,
        ReadOnlyMemory<byte> epochBefore, bool lifecycleStateAbsent, ReadOnlyMemory<byte> epochAfter,
        JsonSerializerOptions serializer)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(serializer);
        try
        {
            if (!lifecycleStateAbsent || epochBefore.Length is 0 or > 32 || !epochBefore.Span.SequenceEqual(epochAfter.Span)
                || JsonSerializer.Deserialize<long>(epochBefore.Span, serializer) <= 0
                || checkpoint.Length is 0 or > 1024 * 1024 || string.IsNullOrWhiteSpace(source.AggregateId)
                || source.Events is not { Count: > 0 and <= 256 }
                || source.Metadata is not { FromSequence: 0, ToSequence: null, IsTruncated: false, NextContinuationToken: null }
                || source.Metadata.LatestSequence != source.Events.Count || source.Metadata.EventCount != source.Events.Count
                || source.Metadata.LastSequenceReturned != source.Metadata.LatestSequence) return false;
            var identity = new AggregateIdentity(source.Tenant, source.Domain, source.AggregateId);
            if (StreamReadPageValidator.ValidateAndGetNextSequence(new(source.Tenant, source.Domain, source.AggregateId,
                FromSequence: 0, PageSize: 256), source) != source.Metadata.LatestSequence
                || source.Events.Any(item => item.MetadataVersion != 1 || string.IsNullOrWhiteSpace(item.CorrelationId)
                    || item.PayloadVersion is < 1 or > 1024
                    || item.ProtectionMetadata is { } protection
                        && (protection.State != PayloadProtectionState.Unprotected || protection.MetadataVersion != 1))) return false;
            ProjectionKeySegments.Validate(projection, nameof(projection));
            using var document = JsonDocument.Parse(checkpoint, new JsonDocumentOptions { MaxDepth = 32 });
            RejectDuplicateProperties(document.RootElement, serializer.PropertyNameCaseInsensitive);
            string Name(string value) => serializer.PropertyNamingPolicy?.ConvertName(value) ?? value;
            foreach (string name in new[] { "SchemaVersion", "WriterProtocolVersion", "TenantId", "Domain", "AggregateId", "ProjectionName",
                "LastDeliveredSequence", "LastCompletedMessageId", "CompletedPrefixFingerprint", "CompletedReceipts", "FirstRetainedSequence",
                "ActiveReservation", "UpdatedAt", "CompletedMessageIdDigests", "IdleBindingPending" })
                if (!document.RootElement.TryGetProperty(Name(name), out _)) return false;
            ProjectionDeliveryStateBody.Validate(checkpoint.Span, serializer, 256);
            var state = document.RootElement.Deserialize<ProjectionDeliveryState>(serializer);
            if (state is null || state.ActiveReservation is not null || state.IdleBindingPending is not null
                || state.LastDeliveredSequence != source.Metadata.LatestSequence || state.FirstRetainedSequence != 1
                || state.CompletedReceipts?.Count != source.Events.Count || state.CompletedMessageIdDigests?.Count != source.Events.Count
                || !ProjectionDeliveryFingerprint.IsCanonical(state.CompletedPrefixFingerprint)) return false;
            bool positionSensitive = state.CompletedPrefixFingerprint!.StartsWith("v2:", StringComparison.Ordinal);
            var events = source.Events.Select(item => new ProjectionEventDto(item.EventTypeName, item.Payload,
                item.SerializationFormat, item.SequenceNumber, item.Timestamp, item.CorrelationId!,
                item.MessageId, item.UserId, item.GlobalPosition)
            { MetadataVersion = item.MetadataVersion, StoredPayloadVersion = item.PayloadVersion }).ToArray();
            ProjectionDeliveryFingerprintHistory history = ProjectionDeliveryFingerprint.ComputeHistory(identity, projection, events, positionSensitive);
            if (!ProjectionDeliveryIdempotencyCoordinator.IsValidCurrentState(state, identity, projection, history.InitialFingerprint)
                || state.CompletedPrefixFingerprint != history.PrefixFingerprint || state.LastCompletedMessageId != events[^1].MessageId) return false;
            for (int index = 0; index < events.Length; index++)
            {
                ProjectionDeliveryReceipt actual = state.CompletedReceipts![index];
                ProjectionDeliveryEventDigest expected = history.Events[index];
                if (actual.SequenceNumber != expected.SequenceNumber || actual.MessageId != expected.MessageId
                    || actual.EventFingerprint != expected.EventFingerprint || actual.PrefixFingerprint != expected.PrefixFingerprint
                    || !ProjectionMessageIdDigest.TryParse(state.CompletedMessageIdDigests![index], out var digest)
                    || digest != ProjectionMessageIdDigest.FromMessageId(expected.MessageId)) return false;
            }
            if (state.SchemaVersion == ProjectionDeliveryState.GuardSchemaVersion)
            {
                long epoch = JsonSerializer.Deserialize<long>(epochBefore.Span, serializer);
                var guard = state.GuardEvidence;
                var selection = guard?.OwnerProof?.Selection;
                if (state.GuardEvidenceVersion != 1 || guard is null || guard.Source is null || guard.IdleLifecycleRevision != epoch
                    || !SourceProofCodec.IsValid(guard.Source, identity, state.LastDeliveredSequence, guard.Source.RetainedFloor)
                    || guard.Source.TenantId != source.Tenant || guard.Source.Domain != source.Domain || guard.Source.AggregateId != source.AggregateId
                    || guard.Source.HeadSequence != state.LastDeliveredSequence || guard.Source.HeadMessageId != state.LastCompletedMessageId
                    || guard.Source.Erased || selection is null || selection.TenantId != source.Tenant || selection.Domain != source.Domain
                    || selection.AggregateId != source.AggregateId || selection.ProjectionName != projection
                    || selection.AppliedSequence != state.LastDeliveredSequence || selection.MessageId != state.LastCompletedMessageId
                    || !ProjectionDeliveryIdempotencyCoordinator.ValidOwnerProof(guard.OwnerProof, identity, projection)) return false;
            }
            return true;
        }
        catch (Exception failure) when (failure is JsonException or ArgumentException or InvalidOperationException or OverflowException) { return false; }
    }

    private static void RejectDuplicateProperties(JsonElement value, bool ignoreCase)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Duplicate delivery observation property.");
                RejectDuplicateProperties(property.Value, ignoreCase);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in value.EnumerateArray()) RejectDuplicateProperties(item, ignoreCase);
    }
}
