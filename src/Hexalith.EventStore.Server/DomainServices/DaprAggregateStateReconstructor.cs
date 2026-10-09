using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Client.Events;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>
/// Dapr-backed aggregate state reconstructor. Resolves the owning domain service via
/// <see cref="IDomainServiceResolver"/> and invokes its <c>POST /replay-state</c> endpoint
/// so aggregate replay always runs the Apply convention owned by the domain. This is the
/// only entry point allowed for Admin aggregate state inspection per the
/// admin-ui-aggregate-state-replay-correctness story.
/// </summary>
public sealed class DaprAggregateStateReconstructor(
    DaprClient daprClient,
    IHttpClientFactory httpClientFactory,
    IDomainServiceResolver resolver,
    ILogger<DaprAggregateStateReconstructor> logger) : IAggregateStateReconstructor {
    /// <summary>The replay endpoint method name registered by domain services.</summary>
    public const string ReplayStateMethodName = "replay-state";

    private const string DefaultReplayVersion = "v1";

    /// <inheritdoc/>
    public async Task<AggregateReconstructionResult> ReconstructAsync(
        AggregateIdentity identity,
        string aggregateType,
        IReadOnlyList<EventEnvelope> events,
        long upToSequence,
        bool includeTimeline = false,
        string? requestId = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(events);

        if (upToSequence < 0) {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.Unexpected,
                "UpToSequence must be >= 0.");
        }

        // The domain service resolves known event versions before applying them.
        foreach (EventEnvelope source in events) {
            cancellationToken.ThrowIfCancellationRequested();
            if (source.SequenceNumber <= upToSequence
                && (source.MetadataVersion != 1 || source.EventContractType is not null
                    || source.PayloadVersion is < 1 or > 1024)) {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.UnsupportedVersion,
                    "Replay source has unsupported event metadata.",
                    failedSequenceNumber: source.SequenceNumber);
            }
        }

        string replayVersion;
        try {
            replayVersion = ResolveReplayVersion(events, upToSequence);
        }
        catch (ArgumentException) {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.UnsupportedVersion,
                "Domain service version metadata is not supported for replay.",
                failedSequenceNumber: upToSequence);
        }

        DomainServiceRegistration? registration;
        try {
            registration = await resolver
                .ResolveAsync(identity.TenantId, identity.Domain, replayVersion, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DomainServiceException ex) {
            logger.LogWarning(
                ex,
                "Domain service registration lookup failed during replay: Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}",
                identity.TenantId,
                identity.Domain,
                identity.AggregateId);
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.UnknownAggregateType,
                $"Domain service registration for tenant '{identity.TenantId}', domain '{identity.Domain}' is not resolvable.");
        }

        if (registration is null) {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.UnknownAggregateType,
                $"No domain service is registered for tenant '{identity.TenantId}', domain '{identity.Domain}'.");
        }

        // Build the replay wire payload. Replay envelopes carry only the metadata the Apply
        // path consumes plus diagnostics fields; sensitive fields (UserId) are intentionally
        // omitted so replay traffic does not duplicate identity information.
        var wireEvents = new ReplayEventEnvelope[events.Count];
        for (int i = 0; i < events.Count; i++) {
            EventEnvelope source = events[i];
            wireEvents[i] = new ReplayEventEnvelope(
                SequenceNumber: source.SequenceNumber,
                EventTypeName: source.EventTypeName,
                Payload: source.Payload,
                SerializationFormat: source.SerializationFormat,
                MetadataVersion: source.MetadataVersion,
                MessageId: source.MessageId,
                CorrelationId: string.IsNullOrWhiteSpace(source.CorrelationId) ? null : source.CorrelationId,
                CausationId: string.IsNullOrWhiteSpace(source.CausationId) ? null : source.CausationId)
            {
                StoredPayloadVersion = source.PayloadVersion,
            };
        }

        AggregateReconstructionRequest request = new(
            TenantId: identity.TenantId,
            Domain: identity.Domain,
            AggregateType: aggregateType ?? string.Empty,
            AggregateId: identity.AggregateId,
            UpToSequence: upToSequence,
            Events: wireEvents,
            IncludeTimeline: includeTimeline,
            RequestId: requestId);

        try {
            using HttpRequestMessage httpRequest = daprClient.CreateInvokeMethodRequest(
                registration.AppId,
                ReplayStateMethodName,
                request);
            HttpClient httpClient = httpClientFactory.CreateClient();
            using HttpResponseMessage httpResponse = await httpClient
                .SendAsync(httpRequest, cancellationToken)
                .ConfigureAwait(false);

            if (httpResponse.StatusCode == HttpStatusCode.NotFound) {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.UnknownAggregateType,
                    $"Domain service '{registration.AppId}' has no '{ReplayStateMethodName}' endpoint for tenant '{identity.TenantId}', domain '{identity.Domain}'.");
            }

            if (!httpResponse.IsSuccessStatusCode) {
                long? contentLength = httpResponse.Content.Headers.ContentLength;
                logger.LogWarning(
                    "Replay invocation returned non-success: AppId={AppId}, Status={StatusCode}, Tenant={TenantId}, Domain={Domain}, RequestId={RequestId}, ContentLength={ContentLength}",
                    registration.AppId,
                    (int)httpResponse.StatusCode,
                    identity.TenantId,
                    identity.Domain,
                    requestId,
                    contentLength);
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.Unexpected,
                    $"Replay invocation returned HTTP {(int)httpResponse.StatusCode}.");
            }

            AggregateReconstructionResult? result = await httpResponse.Content
                .ReadFromJsonAsync<AggregateReconstructionResult>(cancellationToken)
                .ConfigureAwait(false);

            if (result is null) {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.Unexpected,
                    $"Replay endpoint returned an empty response (Tenant '{identity.TenantId}', Domain '{identity.Domain}').");
            }

            return result;
        }
        catch (JsonException ex) {
            logger.LogWarning(
                ex,
                "Replay response could not be deserialized: AppId={AppId}, Tenant={TenantId}, Domain={Domain}, RequestId={RequestId}",
                registration.AppId,
                identity.TenantId,
                identity.Domain,
                requestId);
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.Unexpected,
                "Replay response could not be deserialized.");
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogWarning(
                ex,
                "Replay invocation failed: AppId={AppId}, Tenant={TenantId}, Domain={Domain}, RequestId={RequestId}",
                registration.AppId,
                identity.TenantId,
                identity.Domain,
                requestId);
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.Unexpected,
                "Replay invocation failed.");
        }
    }

    /// <summary>Reconstructs from an addressed actor range only after the shared production reader accepts it.</summary>
    internal async Task<AggregateReconstructionResult> ReconstructAddressedAsync(
        AggregateIdentity identity,
        string aggregateType,
        DaprProductionLogicalEventReader reader,
        SnapshotManager snapshotReplay,
        long upToSequence,
        bool includeTimeline = false,
        string? requestId = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(snapshotReplay);
        if (upToSequence <= 0) {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.Unexpected,
                "UpToSequence must be positive for an addressed logical replay.");
        }

        if (upToSequence > int.MaxValue) {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.Unexpected,
                "UpToSequence exceeds the addressed logical page budget.");
        }

        DaprProductionLogicalReplay replay;
        try {
            replay = await snapshotReplay.ReadReplayRangeAsync(
                reader,
                identity,
                aggregateType,
                startSequence: 1,
                count: (int)upToSequence,
                cancellationToken,
                expectedActorHead: upToSequence).ConfigureAwait(false);
            if (replay.Evolved) {
                replay.Dispose();
                throw new InvalidOperationException("CapabilityMismatch: evolved replay requires a verified effective event route.");
            }
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (ProtectedDataUnreadableException) {
            throw;
        }
        catch (MissingEventException exception) {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.Unexpected,
                "The addressed logical prefix is incomplete.",
                failedSequenceNumber: exception.SequenceNumber);
        }
        catch (InvalidOperationException exception) {
            AggregateReconstructionErrorCategory category = exception.Message.Contains("UnknownEventContract", StringComparison.Ordinal)
                || exception.Message.Contains("CapabilityMismatch", StringComparison.Ordinal)
                || exception.Message.Contains("UpcasterContractViolation", StringComparison.Ordinal)
                || exception.Message.Contains("RollbackReaderCapabilityHold", StringComparison.Ordinal)
                ? AggregateReconstructionErrorCategory.UnsupportedVersion
                : AggregateReconstructionErrorCategory.Unexpected;
            return AggregateReconstructionResult.Failed(category, exception.Message);
        }

        using (replay) {
            return await ReconstructAsync(
                identity,
                aggregateType,
                replay.DomainEvents,
                upToSequence,
                includeTimeline,
                requestId,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Runs dormant event-only fixed-head reconstruction through canonical state/page/final participant readback.</summary>
    /// <remarks>Supplied local owners and keys establish no serving registration, command-state or snapshot authority.</remarks>
    internal static async Task<AggregateReconstructionResult> ReconstructAddressedAsync(AggregateIdentity identity,
        string aggregateType, DaprLogicalReplaySource source, DaprLogicalSourceBinding binding,
        DaprLogicalClaimTrust trust, ECDsa signingKey, DaprReplayOperationOwner operation, string ownerId,
        string requestId, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        if (binding.Identity != identity || binding.AggregateType != aggregateType || !operation.HasReconstructionBinding)
        {
            return AggregateReconstructionResult.Failed(AggregateReconstructionErrorCategory.Conflict, "Logical replay scope or canonical binding differs.")
                with
            {
                ReasonCode = "ReplayRestartRequired"
            };
        }
        try
        {
            using DaprReplayOperationResult begin = await operation.BeginAsync(source, binding, trust, ownerId, null, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (begin.Outcome != DaprReplayCommitOutcome.Proven || begin.ResponseUnavailable)
            {
                return AggregateReconstructionResult.Failed(AggregateReconstructionErrorCategory.Hold, "Canonical replay admission is unavailable.")
                    with
                {
                    ReasonCode = "ReplayRestartRequired"
                };
            }
            for (long ordinal = 1; ordinal <= 65536; ordinal++)
            {
                using DaprReplayOperationResult page = await operation.ExecutePageAsync(source, binding, trust, signingKey,
                    ownerId, begin.Generation, ordinal, requestId, pageSize, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (page.Outcome != DaprReplayCommitOutcome.Proven || page.Response is null || page.CanonicalState is null)
                {
                    return AggregateReconstructionResult.Failed(AggregateReconstructionErrorCategory.Hold, "Canonical replay readback is unavailable.")
                        with
                    {
                        ReasonCode = "ReplayRestartRequired"
                    };
                }
                if (page.IsComplete)
                {
                    await source.RequireCurrentAsync(binding, trust, cancellationToken).ConfigureAwait(false);
                    string json = new UTF8Encoding(false, true).GetString(page.CanonicalState.Bytes.Span);
                    operation.RequireReconstructionCurrent(binding, cancellationToken);
                    await source.RequireCurrentAsync(binding, trust, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    trust.RequireCurrent(cancellationToken);
                    return AggregateReconstructionResult.Succeeded(json, binding.TargetSequence);
                }
            }
            return AggregateReconstructionResult.Failed(AggregateReconstructionErrorCategory.Limit, "Canonical replay exceeds the operation page limit.") with
            {
                ReasonCode = "ProofLimit"
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DaprLogicalReplayApplyException exception)
        {
            return AggregateReconstructionResult.Failed(AggregateReconstructionErrorCategory.ApplyFailed,
                "Apply failed; the canonical replay page did not commit.", exception.Sequence, exception.CanonicalType)
                with
            {
                ReasonCode = "ApplyFailed"
            };
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return AggregateReconstructionResult.Failed(AggregateReconstructionErrorCategory.Conflict,
                "Canonical replay bytes or codec are invalid.") with
            {
                ReasonCode = "ReplayRestartRequired"
            };
        }
        catch (InvalidOperationException exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (exception.Message.StartsWith("ProofLimit:", StringComparison.Ordinal)
                || exception.Message.StartsWith("ScratchLimit:", StringComparison.Ordinal)
                || exception.Message.StartsWith("ReadableLimit:", StringComparison.Ordinal))
            {
                return AggregateReconstructionResult.Failed(AggregateReconstructionErrorCategory.Limit,
                    "Canonical replay exceeds its admitted bounded capacity.") with
                {
                    ReasonCode = "ProofLimit"
                };
            }
            bool apply = exception.Message.StartsWith("ApplyFailed:", StringComparison.Ordinal);
            return AggregateReconstructionResult.Failed(apply ? AggregateReconstructionErrorCategory.ApplyFailed : AggregateReconstructionErrorCategory.Hold,
                apply ? "Apply failed; the canonical replay page did not commit." : "Canonical replay evidence is unavailable.")
                with
            {
                ReasonCode = apply ? "ApplyFailed" : "ReplayRestartRequired"
            };
        }
    }

    private static string ResolveReplayVersion(IReadOnlyList<EventEnvelope> events, long upToSequence) {
        string version = events
            .Where(e => e.SequenceNumber <= upToSequence)
            .OrderBy(e => e.SequenceNumber)
            .LastOrDefault(e => !string.IsNullOrWhiteSpace(e.DomainServiceVersion))
            ?.DomainServiceVersion
            ?? DefaultReplayVersion;

        string normalized = version.ToLowerInvariant();
        DaprDomainServiceInvoker.ValidateVersionFormat(normalized);
        return normalized;
    }
}
