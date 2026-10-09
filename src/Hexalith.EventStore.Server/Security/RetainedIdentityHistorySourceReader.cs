using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Streams;
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
    private readonly Action<byte[]>? _ownedPayloadObserved;

    internal RetainedIdentityHistorySourceReader(IActorProxyFactory actors, IRetainedIdentityHistoryAdmission admission,
        IIdentityHistoryCustody custody, TimeProvider clock, Action<byte[]> ownedPayloadObserved)
        : this(actors, admission, custody, clock) => _ownedPayloadObserved = ownedPayloadObserved;

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
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
        var ownedClosedPayloads = new List<byte[]>();
        bool transferred = false;
        RetainedIdentityHistoryReadResult Denied(string reason)
        {
            deadline.ThrowIfCancellationRequested();
            return new(null, reason);
        }
        try
        {
            deadline.ThrowIfCancellationRequested();
            RetainedIdentityHistoryGrant? grant = await deadline.ReadAsync(token => admission.AdmitAsync(principal, request, token)).ConfigureAwait(false);
            deadline.ThrowIfCancellationRequested();
            grant = await deadline.ReadAsync(_ => Task.FromResult(CaptureGrant(grant, request, clock.GetUtcNow(), deadline))).ConfigureAwait(false);
            deadline.ThrowIfCancellationRequested();
            if (grant is null || grant.ExpiresAt <= clock.GetUtcNow())
            {
                return Denied("history-denied");
            }

            IAggregateActor actor = actors.CreateActorProxy<IAggregateActor>(new ActorId(request.Identity.ActorId), "AggregateActor");
            AggregateStreamMetadata head = await deadline.ReadAsync(_ => actor.GetStreamMetadataAsync()).ConfigureAwait(false);
            deadline.ThrowIfCancellationRequested();
            if (!head.Exists || head.CurrentSequence is < 0 or > RetainedIdentityHistoryLimits.MaxSourcePositions)
            {
                return Denied("history-unavailable");
            }

            var events = new List<StreamReadEvent>();
            var excluded = new List<long>();
            long cursor = 0;
            long bytes = 0;
            long readableBytes = 0;
            var retainedEvidence = new List<IdentityHistoryCustodyEvidence>();
            var expiredEvents = new List<(ExpiredIdentityHistoryCertificate Certificate, byte[] Payload, string Format)>();
            while (cursor < head.CurrentSequence)
            {
                EventEnvelope[] page = await deadline.ReadAsync(_ => actor.ReadEventsRangeAsync(cursor, head.CurrentSequence, 100)).ConfigureAwait(false);
                deadline.ThrowIfCancellationRequested();
                if (page.Length is 0 or > 100)
                {
                    return Denied("history-source-gap");
                }

                foreach (EventEnvelope item in page)
                {
                    deadline.ThrowIfCancellationRequested();
                    if (item is null || item.Identity != request.Identity || item.SequenceNumber != cursor + 1
                        || item.SequenceNumber > head.CurrentSequence || item.Payload is null)
                    {
                        return Denied("history-source-gap-or-scope-mismatch");
                    }

                    if (item.MetadataVersion != 1 || item.EventContractType is not null || item.PayloadVersion is not null
                        || item.SerializationFormat is not ("json" or "json+pdenc-v1" or "json+identity-history-v1"))
                    {
                        return Denied("history-source-metadata-unsupported");
                    }

                    cursor++;
                    bytes += item.Payload.Length;
                    if (bytes > RetainedIdentityHistoryLimits.MaxPayloadBytes)
                    {
                        return Denied("history-source-bound-exceeded");
                    }

                    Type? type = grant!.EventTypes.SingleOrDefault(candidate => item.EventTypeName == candidate.FullName);
                    if (type is null)
                    {
                        if (!grant.ExcludedEventTypeNames.Contains(item.EventTypeName, StringComparer.Ordinal))
                        {
                            return Denied("history-source-contract-unavailable");
                        }

                        excluded.Add(item.SequenceNumber);
                        continue;
                    }

                    if (EventStorePayloadProtectionMetadataCarrier.Read(item.Extensions).State != PayloadProtectionState.Protected)
                    {
                        return Denied("history-source-protection-missing");
                    }

                    // Retain only detached existing ciphertext metadata for actor-free continuity.
                    // An optional independently qualified terminal receipt must precede any skip;
                    // failure/null never turns an unreadable predecessor into a profile exclusion.
                    byte[] sealedPayload = item.Payload.ToArray();
                    if (custody is IExpiredIdentityHistoryCustody expiredCustody)
                    {
                        var expired = await deadline.ReadAsync(token => expiredCustody.ReadExpiredAsync(request.Identity,
                            item.EventTypeName, item.SequenceNumber, sealedPayload.ToArray(), item.SerializationFormat, token)).ConfigureAwait(false);
                        deadline.ThrowIfCancellationRequested();
                        if (expired is not null)
                        {
                            if (!ValidExpired(expired, request, item.EventTypeName, item.SequenceNumber, sealedPayload, clock.GetUtcNow()))
                            { return Denied("history-expired-proof-unavailable"); }
                            expiredEvents.Add((expired, sealedPayload, item.SerializationFormat));
                            continue;
                        }
                    }
                    PayloadProtectionResult readable = await deadline.ReadAsync(token => custody.UnprotectEventAsync(request.Identity, item.EventTypeName,
                        sealedPayload.ToArray(), item.SerializationFormat, token),
                        abandonedResultCleanup: static value => CryptographicOperations.ZeroMemory(value.PayloadBytes)).ConfigureAwait(false);
                    try
                    {
                        deadline.ThrowIfCancellationRequested();
                        readableBytes += readable.PayloadBytes.Length;
                        if (readable.Metadata.State != PayloadProtectionState.Unprotected
                            || readable.SerializationFormat != "json"
                            || readableBytes > RetainedIdentityHistoryLimits.MaxPayloadBytes
                            || JsonSerializer.Deserialize(readable.PayloadBytes, type, _jsonOptions) is not IIdentityHistoryEvent value
                            || value.Custody is not { SourceExpiryEnforced: true, RestoreSafe: true, DerivedCopiesCovered: true, LifecycleRevision: > 0 } evidence
                            || string.IsNullOrWhiteSpace(evidence.PolicyId) || string.IsNullOrWhiteSpace(evidence.EvidenceId)
                            || evidence.Purpose != request.Purpose || evidence.ExpiresAt <= clock.GetUtcNow())
                        {
                            return Denied("history-custody-unavailable-or-expired");
                        }
    
                        bool allowed = await deadline.ReadAsync(token => custody.CanReadAsync(request.Identity, evidence, token)).ConfigureAwait(false);
                        deadline.ThrowIfCancellationRequested();
                        if (!allowed)
                        {
                            return Denied("history-custody-unavailable-or-expired");
                        }
    
                        retainedEvidence.Add(evidence);
                        byte[] closedPayload = JsonSerializer.SerializeToUtf8Bytes(value, type, _jsonOptions);
                        ownedClosedPayloads.Add(closedPayload);
                        _ownedPayloadObserved?.Invoke(closedPayload);
                        events.Add(new StreamReadEvent(item.SequenceNumber, item.EventTypeName, closedPayload,
                            readable.SerializationFormat, item.MetadataVersion, string.Empty, null, null, item.Timestamp, null,
                            EventStorePayloadProtectionMetadata.Unprotected()));
                    }
                    finally { CryptographicOperations.ZeroMemory(readable.PayloadBytes); }
                }
            }

            AggregateStreamMetadata confirmed = await deadline.ReadAsync(_ => actor.GetStreamMetadataAsync()).ConfigureAwait(false);
            deadline.ThrowIfCancellationRequested();
            RetainedIdentityHistoryGrant? finalGrant = await deadline.ReadAsync(token => admission.AdmitAsync(principal, request, token)).ConfigureAwait(false);
            deadline.ThrowIfCancellationRequested();
            finalGrant = await deadline.ReadAsync(_ => Task.FromResult(CaptureGrant(finalGrant, request, clock.GetUtcNow(), deadline))).ConfigureAwait(false);
            deadline.ThrowIfCancellationRequested();
            if (confirmed != head || finalGrant is null || finalGrant.ExpiresAt <= clock.GetUtcNow()
                || finalGrant!.AuthorityRevision != grant!.AuthorityRevision || finalGrant.ExpiresAt != grant.ExpiresAt
                || !finalGrant.EventTypes.SequenceEqual(grant.EventTypes)
                || !finalGrant.ExcludedEventTypeNames.SequenceEqual(grant.ExcludedEventTypeNames)
                || retainedEvidence.Any(evidence => evidence.ExpiresAt <= clock.GetUtcNow()))
            {
                return Denied("history-source-or-authority-changed");
            }

            // Lifecycle validation must follow the awaited source/authority checks. The owner provider
            // is responsible for a current, version-bound release observation, including restore fences.
            foreach (IdentityHistoryCustodyEvidence evidence in retainedEvidence)
            {
                bool allowed = await deadline.ReadAsync(token => custody.CanReadAsync(request.Identity, evidence, token)).ConfigureAwait(false);
                deadline.ThrowIfCancellationRequested();
                if (!allowed)
                {
                    return Denied("history-custody-unavailable-or-expired");
                }
            }

            foreach (var expired in expiredEvents)
            {
                var cert = expired.Certificate;
                var final = await deadline.ReadAsync(token => ((IExpiredIdentityHistoryCustody)custody).ReadExpiredAsync(request.Identity,
                    cert.EventTypeName, cert.SourceSequence, expired.Payload.ToArray(), expired.Format, token)).ConfigureAwait(false);
                deadline.ThrowIfCancellationRequested();
                if (final is null || !ValidExpired(final, request, cert.EventTypeName, cert.SourceSequence, expired.Payload, clock.GetUtcNow())
                    || final with { ObservedAt = cert.ObservedAt } != cert)
                { return Denied("history-expired-proof-changed"); }
            }

            DateTimeOffset observedAt = clock.GetUtcNow();
            DateTimeOffset validUntil = retainedEvidence.Aggregate(finalGrant.ExpiresAt,
                (earliest, evidence) => evidence.ExpiresAt < earliest ? evidence.ExpiresAt : earliest);
            validUntil = expiredEvents.Aggregate(validUntil,
                (earliest, expired) => expired.Certificate.ValidUntil < earliest ? expired.Certificate.ValidUntil : earliest);
            var stream = new RetainedIdentityHistoryStream(request.Identity, request.Purpose, head.CurrentSequence,
                observedAt, events.AsReadOnly(), excluded.AsReadOnly(), Convert.ToHexString(RandomNumberGenerator.GetBytes(16)))
            {
                AuthorityRevision = finalGrant.AuthorityRevision,
                ValidUntil = validUntil,
                ExpiredEvents = Array.AsReadOnly(expiredEvents.Select(e => e.Certificate).ToArray()),
            };
            var result = new RetainedIdentityHistoryReadResult(stream, null);
            bool complete = RetainedIdentityHistoryValidator.IsComplete(request, stream, clock.GetUtcNow());
            bool bounded = false;
            if (complete)
            {
                byte[] probe = JsonSerializer.SerializeToUtf8Bytes(result, _jsonOptions);
                try
                {
                    _ownedPayloadObserved?.Invoke(probe);
                    bounded = probe.Length <= RetainedIdentityHistoryLimits.MaxResponseBytes;
                }
                finally { CryptographicOperations.ZeroMemory(probe); }
            }
            bool valid = bounded && validUntil > clock.GetUtcNow();
            deadline.ThrowIfCancellationRequested();
            if (!valid) { return Denied("history-source-incomplete-or-expired"); }
            transferred = true;
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(null, deadline.IsExpired ? "history-time-bound-exceeded" : "history-unavailable");
        }
        catch (OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(null, "history-unavailable");
        }
        finally
        {
            if (!transferred)
            {
                foreach (byte[] payload in ownedClosedPayloads) { CryptographicOperations.ZeroMemory(payload); }
            }
        }
    }

    private static bool ValidExpired(ExpiredIdentityHistoryCertificate certificate, RetainedIdentityHistoryReadRequest request,
        string type, long position, byte[] sealedPayload, DateTimeOffset now)
        => certificate.ContractVersion == 1 && certificate.Identity == request.Identity && certificate.Purpose == request.Purpose
            && !string.IsNullOrWhiteSpace(certificate.PolicyId) && certificate.PolicyId.Length <= 2048
            && certificate.SourceSequence == position && certificate.EventTypeName == type
            && certificate.SealedPayloadDigest == Convert.ToHexString(SHA256.HashData(sealedPayload))
            && certificate.LifecycleRevision > 0 && certificate.ObservedAt != default && certificate.ObservedAt <= now && certificate.ValidUntil > now
            && !string.IsNullOrWhiteSpace(certificate.AuthorityRevision) && certificate.AuthorityRevision.Length <= 2048
            && !string.IsNullOrWhiteSpace(certificate.DestructionReceiptId) && certificate.DestructionReceiptId.Length <= 2048;

    private static RetainedIdentityHistoryGrant? CaptureGrant(RetainedIdentityHistoryGrant? grant,
        RetainedIdentityHistoryReadRequest request, DateTimeOffset now, AuthoritativeStreamReadDeadline deadline)
    {
        deadline.ThrowIfCancellationRequested();
        if (grant?.EventTypes is null || grant.ExcludedEventTypeNames is null) { return null; }
        int typeCount = grant.EventTypes.Count;
        int excludedCount = grant.ExcludedEventTypeNames.Count;
        if (typeCount is < 1 or > 32 || excludedCount is < 0 or > 512) { return null; }
        var types = new List<Type>(typeCount);
        foreach (Type type in grant.EventTypes)
        {
            deadline.ThrowIfCancellationRequested();
            if (types.Count >= typeCount) { return null; }
            types.Add(type);
        }
        var excluded = new List<string>(excludedCount);
        foreach (string name in grant.ExcludedEventTypeNames)
        {
            deadline.ThrowIfCancellationRequested();
            if (excluded.Count >= excludedCount) { return null; }
            excluded.Add(name);
        }
        deadline.ThrowIfCancellationRequested();
        if (types.Count != typeCount || excluded.Count != excludedCount) { return null; }
        var captured = grant with { EventTypes = types.AsReadOnly(), ExcludedEventTypeNames = excluded.AsReadOnly() };
        return ValidGrant(captured, request, now) ? captured : null;
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
