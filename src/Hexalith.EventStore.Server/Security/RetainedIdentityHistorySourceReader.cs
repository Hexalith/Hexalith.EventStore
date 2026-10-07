using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Reads sealed source positions and decrypts only admitted independently retained attribution.</summary>
/// <param name="actors">The SDK actor transport.</param>
/// <param name="admission">Current exact-source retained-purpose authorization.</param>
/// <param name="custody">The independent finite history lifecycle and protection owner.</param>
/// <param name="clock">The authoritative observation clock.</param>
public sealed class RetainedIdentityHistorySourceReader(IActorProxyFactory actors, IRetainedIdentityHistoryAdmission admission,
    IIdentityHistoryCustody custody, TimeProvider clock)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Observes a complete stable partition; no partial response or profile plaintext is returned.</summary>
    public async Task<RetainedIdentityHistoryReadResult> ReadAsync(ClaimsPrincipal principal,
        RetainedIdentityHistoryReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(request);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        CancellationToken readToken = deadline.Token;
        try
        {
            readToken.ThrowIfCancellationRequested();
            RetainedIdentityHistoryGrant? grant = await admission.AdmitAsync(principal, request, readToken)
                .WaitAsync(readToken).ConfigureAwait(false);
            readToken.ThrowIfCancellationRequested();
            if (!ValidGrant(grant, request, clock.GetUtcNow()))
            {
                return new(null, "history-denied");
            }

            IAggregateActor actor = actors.CreateActorProxy<IAggregateActor>(new ActorId(request.Identity.ActorId), "AggregateActor");
            AggregateStreamMetadata head = await actor.GetStreamMetadataAsync().WaitAsync(readToken).ConfigureAwait(false);
            readToken.ThrowIfCancellationRequested();
            if (!head.Exists || head.CurrentSequence is < 0 or > RetainedIdentityHistoryLimits.MaxSourcePositions)
            {
                return new(null, "history-unavailable");
            }

            var events = new List<StreamReadEvent>();
            var excluded = new List<long>();
            long cursor = 0;
            long bytes = 0;
            long readableBytes = 0;
            var retainedEvidence = new List<IdentityHistoryCustodyEvidence>();
            while (cursor < head.CurrentSequence)
            {
                EventEnvelope[] page = await actor.ReadEventsRangeAsync(cursor, head.CurrentSequence, 100)
                    .WaitAsync(readToken).ConfigureAwait(false);
                readToken.ThrowIfCancellationRequested();
                if (page.Length is 0 or > 100)
                {
                    return new(null, "history-source-gap");
                }

                foreach (EventEnvelope item in page)
                {
                    readToken.ThrowIfCancellationRequested();
                    if (item is null || item.Identity != request.Identity || item.SequenceNumber != cursor + 1
                        || item.SequenceNumber > head.CurrentSequence || item.Payload is null)
                    {
                        return new(null, "history-source-gap-or-scope-mismatch");
                    }

                    if (item.MetadataVersion != 1 || item.EventContractType is not null || item.PayloadVersion is not null
                        || item.SerializationFormat is not ("json" or "json+pdenc-v1" or "json+identity-history-v1"))
                    {
                        return new(null, "history-source-metadata-unsupported");
                    }

                    cursor++;
                    bytes += item.Payload.Length;
                    if (bytes > RetainedIdentityHistoryLimits.MaxPayloadBytes)
                    {
                        return new(null, "history-source-bound-exceeded");
                    }

                    Type? type = grant!.EventTypes.SingleOrDefault(candidate => item.EventTypeName == candidate.FullName);
                    if (type is null)
                    {
                        if (!grant.ExcludedEventTypeNames.Contains(item.EventTypeName, StringComparer.Ordinal))
                        {
                            return new(null, "history-source-contract-unavailable");
                        }

                        excluded.Add(item.SequenceNumber);
                        continue;
                    }

                    if (EventStorePayloadProtectionMetadataCarrier.Read(item.Extensions).State != PayloadProtectionState.Protected)
                    {
                        return new(null, "history-source-protection-missing");
                    }

                    PayloadProtectionResult readable = await custody.UnprotectEventAsync(request.Identity, item.EventTypeName,
                        item.Payload.ToArray(), item.SerializationFormat, readToken).WaitAsync(readToken).ConfigureAwait(false);
                    readToken.ThrowIfCancellationRequested();
                    readableBytes += readable.PayloadBytes.Length;
                    if (readable.Metadata.State != PayloadProtectionState.Unprotected
                        || readable.SerializationFormat != "json"
                        || readableBytes > RetainedIdentityHistoryLimits.MaxPayloadBytes
                        || JsonSerializer.Deserialize(readable.PayloadBytes, type, _jsonOptions) is not IIdentityHistoryEvent value
                        || value.Custody is not { SourceExpiryEnforced: true, RestoreSafe: true, DerivedCopiesCovered: true, LifecycleRevision: > 0 } evidence
                        || string.IsNullOrWhiteSpace(evidence.PolicyId) || string.IsNullOrWhiteSpace(evidence.EvidenceId)
                        || evidence.Purpose != request.Purpose || evidence.ExpiresAt <= clock.GetUtcNow())
                    {
                        return new(null, "history-custody-unavailable-or-expired");
                    }

                    bool allowed = await custody.CanReadAsync(request.Identity, evidence, readToken)
                        .WaitAsync(readToken).ConfigureAwait(false);
                    readToken.ThrowIfCancellationRequested();
                    if (!allowed)
                    {
                        return new(null, "history-custody-unavailable-or-expired");
                    }

                    retainedEvidence.Add(evidence);
                    byte[] closedPayload = JsonSerializer.SerializeToUtf8Bytes(value, type, _jsonOptions);
                    events.Add(new StreamReadEvent(item.SequenceNumber, item.EventTypeName, closedPayload,
                        readable.SerializationFormat, item.MetadataVersion, string.Empty, null, null, item.Timestamp, null,
                        EventStorePayloadProtectionMetadata.Unprotected()));
                }
            }

            AggregateStreamMetadata confirmed = await actor.GetStreamMetadataAsync().WaitAsync(readToken).ConfigureAwait(false);
            readToken.ThrowIfCancellationRequested();
            RetainedIdentityHistoryGrant? finalGrant = await admission.AdmitAsync(principal, request, readToken)
                .WaitAsync(readToken).ConfigureAwait(false);
            readToken.ThrowIfCancellationRequested();
            if (confirmed != head || !ValidGrant(finalGrant, request, clock.GetUtcNow())
                || finalGrant!.AuthorityRevision != grant!.AuthorityRevision || finalGrant.ExpiresAt != grant.ExpiresAt
                || !finalGrant.EventTypes.SequenceEqual(grant.EventTypes)
                || !finalGrant.ExcludedEventTypeNames.SequenceEqual(grant.ExcludedEventTypeNames)
                || retainedEvidence.Any(evidence => evidence.ExpiresAt <= clock.GetUtcNow()))
            {
                return new(null, "history-source-or-authority-changed");
            }

            // Lifecycle validation must follow the awaited source/authority checks. The owner provider
            // is responsible for a current, version-bound release observation, including restore fences.
            foreach (IdentityHistoryCustodyEvidence evidence in retainedEvidence)
            {
                bool allowed = await custody.CanReadAsync(request.Identity, evidence, readToken)
                    .WaitAsync(readToken).ConfigureAwait(false);
                readToken.ThrowIfCancellationRequested();
                if (!allowed)
                {
                    return new(null, "history-custody-unavailable-or-expired");
                }
            }

            DateTimeOffset observedAt = clock.GetUtcNow();
            DateTimeOffset validUntil = retainedEvidence.Aggregate(finalGrant.ExpiresAt,
                (earliest, evidence) => evidence.ExpiresAt < earliest ? evidence.ExpiresAt : earliest);
            var stream = new RetainedIdentityHistoryStream(request.Identity, request.Purpose, head.CurrentSequence,
                observedAt, events.AsReadOnly(), excluded.AsReadOnly(), Convert.ToHexString(RandomNumberGenerator.GetBytes(16)))
            {
                AuthorityRevision = finalGrant.AuthorityRevision,
                ValidUntil = validUntil,
            };
            var result = new RetainedIdentityHistoryReadResult(stream, null);
            bool complete = RetainedIdentityHistoryValidator.IsComplete(request, stream, clock.GetUtcNow());
            bool bounded = complete && JsonSerializer.SerializeToUtf8Bytes(result, _jsonOptions).Length <= RetainedIdentityHistoryLimits.MaxResponseBytes;
            bool valid = bounded && validUntil > clock.GetUtcNow();
            readToken.ThrowIfCancellationRequested();
            return valid ? result : new(null, "history-source-incomplete-or-expired");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(null, "history-time-bound-exceeded");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new(null, "history-unavailable");
        }
    }

    private static bool ValidGrant(RetainedIdentityHistoryGrant? grant, RetainedIdentityHistoryReadRequest request, DateTimeOffset now)
        => grant is not null && request.Identity is not null && grant.Identity == request.Identity
            && request.Purpose == RetainedIdentityHistoryReadRequest.AttributionPurpose && grant.Purpose == request.Purpose
            && !string.IsNullOrWhiteSpace(grant.AuthorityRevision) && grant.ExpiresAt > now
            && grant.EventTypes is { Count: > 0 and <= 32 }
            && grant.EventTypes.Distinct().Count() == grant.EventTypes.Count
            && grant.EventTypes.Select(type => type?.FullName).Distinct(StringComparer.Ordinal).Count() == grant.EventTypes.Count
            && grant.EventTypes.All(type => type is not null && !type.IsAbstract && !type.IsInterface
                && type.FullName is { Length: > 0 and <= RetainedIdentityHistoryLimits.MaxContractNameLength }
                && typeof(IIdentityHistoryEvent).IsAssignableFrom(type))
            && grant.ExcludedEventTypeNames is { Count: <= 512 }
            && grant.ExcludedEventTypeNames.Distinct(StringComparer.Ordinal).Count() == grant.ExcludedEventTypeNames.Count
            && grant.ExcludedEventTypeNames.All(name => !string.IsNullOrWhiteSpace(name)
                && name.Length <= RetainedIdentityHistoryLimits.MaxContractNameLength
                && !grant.EventTypes.Any(type => type.FullName == name));
}
