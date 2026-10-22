using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>Verifies an authenticated filtered certificate without mistaking sparse events for contiguity.</summary>
public static class RetainedIdentityHistoryValidator
{
    /// <summary>Checks exact scope/purpose, bounds and an exhaustive disjoint original-position partition.</summary>
    /// <param name="request">The exact request sent through authenticated transport.</param>
    /// <param name="stream">The returned source certificate.</param>
    /// <param name="evaluatedAt">The consuming clock; observation validity is exclusive and never renewed locally.</param>
    /// <returns>Whether the certificate is complete and structurally safe.</returns>
    public static bool IsComplete(RetainedIdentityHistoryReadRequest request, RetainedIdentityHistoryStream? stream, DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (stream is null || stream.Identity is null || request.Identity is null
            || stream.Identity != request.Identity || stream.Purpose != request.Purpose
            || request.Purpose != RetainedIdentityHistoryReadRequest.AttributionPurpose
            || stream.Head is < 0 or > RetainedIdentityHistoryLimits.MaxSourcePositions || stream.ObservedAt == default
            || evaluatedAt == default || stream.ObservedAt > evaluatedAt || stream.ValidUntil <= evaluatedAt
            || string.IsNullOrWhiteSpace(stream.AuthorityRevision)
            || string.IsNullOrWhiteSpace(stream.ObservationId) || stream.Events is null || stream.ExcludedSequences is null
            || stream.ExpiredEvents is null
            || stream.Events.Count + (long)stream.ExcludedSequences.Count + stream.ExpiredEvents.Count != stream.Head)
        {
            return false;
        }

        var covered = new HashSet<long>();
        long previous = 0;
        long bytes = 0;
        foreach (StreamReadEvent item in stream.Events)
        {
            if (item is null || item.SequenceNumber <= previous || item.SequenceNumber > stream.Head
                || !covered.Add(item.SequenceNumber) || item.Payload is null || string.IsNullOrWhiteSpace(item.EventTypeName)
                || item.EventTypeName.Length > RetainedIdentityHistoryLimits.MaxContractNameLength
                || item.MessageId != string.Empty || item.UserId is not null || item.CorrelationId is not null || item.CausationId is not null
                || item.SerializationFormat != "json" || item.ProtectionMetadata is not { State: PayloadProtectionState.Unprotected })
            {
                return false;
            }

            bytes += item.Payload.Length;
            if (bytes > RetainedIdentityHistoryLimits.MaxPayloadBytes)
            {
                return false;
            }

            previous = item.SequenceNumber;
        }

        previous = 0;
        foreach (ExpiredIdentityHistoryCertificate certificate in stream.ExpiredEvents)
        {
            if (certificate is null || certificate.ContractVersion != 1 || certificate.Identity != request.Identity
                || string.IsNullOrWhiteSpace(certificate.PolicyId) || certificate.PolicyId.Length > 2048
                || certificate.Purpose != request.Purpose || certificate.SourceSequence <= previous || certificate.SourceSequence > stream.Head
                || !covered.Add(certificate.SourceSequence) || certificate.LifecycleRevision <= 0
                || certificate.ObservedAt == default || certificate.ObservedAt > evaluatedAt || certificate.ValidUntil <= evaluatedAt
                || certificate.ValidUntil < stream.ValidUntil || string.IsNullOrWhiteSpace(certificate.EventTypeName)
                || certificate.EventTypeName.Length > RetainedIdentityHistoryLimits.MaxContractNameLength
                || string.IsNullOrWhiteSpace(certificate.DestructionReceiptId) || certificate.DestructionReceiptId.Length > 2048
                || string.IsNullOrWhiteSpace(certificate.AuthorityRevision) || certificate.AuthorityRevision.Length > 2048
                || certificate.SealedPayloadDigest is not { Length: 64 } digest || !digest.All(char.IsAsciiHexDigit))
            { return false; }
            previous = certificate.SourceSequence;
        }

        previous = 0;
        foreach (long position in stream.ExcludedSequences)
        {
            if (position <= previous || position > stream.Head || !covered.Add(position))
            {
                return false;
            }

            previous = position;
        }

        return covered.Count == stream.Head;
    }
}
