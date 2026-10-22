using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Projections;

internal sealed partial class ProjectionSourceGuardHistoryReader
{
    /// <summary>Authenticates the complete readable prefix and the unchanged durable checkpoint before preparing a replacement.</summary>
    internal async Task<ProjectionSourceGuardReplayAdmission> AdmitReplayAsync(AggregateIdentity identity, string route,
        ProjectionDeliveryState state, SourceProof head, int pageSize, CancellationToken cancellationToken)
        => await AdmitReplayAsync(identity, route, state, head, pageSize, true, cancellationToken, null, true).ConfigureAwait(false);

    /// <summary>Verifies an already admitted complete prefix without repeating its completed-base source hashing.</summary>
    internal async Task<ProjectionSourceGuardReplayAdmission> AdmitReplayAsync(AggregateIdentity identity, string route,
        ProjectionDeliveryState state, SourceProof head, int pageSize, bool validateCheckpoint, CancellationToken cancellationToken,
        ProjectionEventDto[]? sharedEvents = null, bool retainEvents = false, ProjectionSourceAdmittedHistory? expectedAdmission = null)
    {
        ProjectionDeliveryGuardEvidence? evidence = state.GuardEvidence;
        if (head.RetainedFloor != 1 || state.LastDeliveredSequence > 0 && evidence is null)
            throw new ProjectionDeliveryHistoryValidationException("history_unavailable");
        bool v2 = state.CompletedPrefixFingerprint!.StartsWith("v2:", StringComparison.Ordinal);
        string initial = ProjectionDeliveryFingerprint.ComputeInitial(identity, route, v2);
        string prefix = initial;
        SourceProof chain = SourceProofCodec.CreateSeed(identity, head.SourceIncarnation);
        List<ProjectionEventDto>? readable = retainEvents && sharedEvents is null ? [] : null;
        long readableCount = 0;
        ProjectionSourceAdmittedDigests? admitted = expectedAdmission is null
            ? new(state.LastDeliveredSequence, head.HeadSequence, v2, state.CompletedPrefixFingerprint!) : null;
        if (expectedAdmission is not null && state.CompletedPrefixFingerprint != (expectedAdmission.History.BaseFingerprint
            ?? expectedAdmission.History.InitialFingerprint)) throw new ProjectionDeliveryHistoryValidationException("source_conflict");
        HashSet<string>? ids = new(StringComparer.Ordinal);
        long sourceBytes = 0, wireBytes = 0;
        int pages = 0;
        try
        {
            await foreach (ProjectionSourceGuardPage page in ReadPagesAsync(identity, chain, head, pageSize, cancellationToken).ConfigureAwait(false))
            {
                if (page.StartsRange) pages++;
                sourceBytes += page.SourceBytes;
                wireBytes += page.WireBytes;
                for (int index = 0; index < page.Readable.Length; index++)
                {
                    ProjectionEventDto item = page.Readable[index];
                    if (string.IsNullOrWhiteSpace(item.MessageId) || !ids.Add(item.MessageId))
                        throw new ProjectionDeliveryHistoryValidationException("delivery_identity_conflict");
                    string eventHash = ProjectionDeliveryFingerprint.ComputeEvent(item, v2);
                    prefix = ProjectionDeliveryFingerprint.Extend(prefix, eventHash);
                    if (item.SequenceNumber <= state.LastDeliveredSequence)
                    {
                        if (validateCheckpoint) chain = SourceProofCodec.ExtendVerified(chain, page.Stored[index], page.SourceEventDigests[index]);
                        string idDigest = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(item.MessageId)));
                        if (state.CompletedMessageIdDigests![checked((int)item.SequenceNumber - 1)] != idDigest)
                            throw new ProjectionDeliveryHistoryValidationException("source_conflict");
                        if (item.SequenceNumber >= state.FirstRetainedSequence)
                        {
                            ProjectionDeliveryReceipt receipt = state.CompletedReceipts![checked((int)(item.SequenceNumber - state.FirstRetainedSequence))];
                            if (receipt.MessageId != item.MessageId || receipt.EventFingerprint != eventHash || receipt.PrefixFingerprint != prefix)
                                throw new ProjectionDeliveryHistoryValidationException("source_conflict");
                        }
                        if (item.SequenceNumber == state.LastDeliveredSequence
                            && (validateCheckpoint && chain != evidence!.Source || prefix != state.CompletedPrefixFingerprint))
                            throw new ProjectionDeliveryHistoryValidationException("source_conflict");
                    }
                    else
                    {
                        if (admitted is not null) admitted.Add(item.SequenceNumber, item.MessageId, page.SourceEventDigests[index], eventHash, prefix);
                        else if (expectedAdmission!.Events is not ProjectionSourceAdmittedDigestList<ProjectionSourceAdmittedEvent> expected
                            || !expected.Table.MatchesEvent(item.SequenceNumber, item.MessageId, page.SourceEventDigests[index], eventHash))
                            throw new ProjectionDeliveryHistoryValidationException("source_conflict");
                    }
                    if (sharedEvents is not null && (readableCount >= sharedEvents.LongLength
                        || !SameReadableEvent(item, sharedEvents[checked((int)readableCount)])))
                        throw new ProjectionDeliveryHistoryValidationException("source_conflict");
                    readable?.Add(item);
                    readableCount++;
                }
            }
        }
        finally
        {
            ids = null;
        }
        if (sharedEvents is not null && readableCount != sharedEvents.LongLength)
            throw new ProjectionDeliveryHistoryValidationException("source_conflict");
        if (admitted is null)
        {
            if (prefix != expectedAdmission!.History.PrefixFingerprint
                || expectedAdmission.Events.Count != head.HeadSequence - state.LastDeliveredSequence)
                throw new ProjectionDeliveryHistoryValidationException("source_conflict");
            return new(expectedAdmission, []);
        }
        admitted.Seal();
        return new(new(new(initial, prefix, admitted.RouteEvents, state.LastDeliveredSequence, state.CompletedPrefixFingerprint),
            admitted.SourceEvents, sourceBytes, wireBytes, pages), sharedEvents ?? (readable is null ? [] : [.. readable]));
    }
    private static bool SameReadableEvent(ProjectionEventDto actual, ProjectionEventDto expected)
        => actual.Payload.AsSpan().SequenceEqual(expected.Payload)
            && SameBytes(actual.EffectivePayload, expected.EffectivePayload)
            && SameBytes(actual.StoredDigest, expected.StoredDigest)
            && actual.Timestamp.Offset == expected.Timestamp.Offset
            && actual with { Payload = expected.Payload, EffectivePayload = expected.EffectivePayload,
                StoredDigest = expected.StoredDigest } == expected;

    private static bool SameBytes(byte[]? actual, byte[]? expected)
        => (actual is null) == (expected is null) && actual.AsSpan().SequenceEqual(expected);
}
