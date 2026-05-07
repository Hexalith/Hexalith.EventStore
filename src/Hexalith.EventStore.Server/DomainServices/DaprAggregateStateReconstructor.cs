using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Server.Events;

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
    ILogger<DaprAggregateStateReconstructor> logger) : IAggregateStateReconstructor
{
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(events);

        if (upToSequence < 0)
        {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.Unexpected,
                "UpToSequence must be >= 0.");
        }

        string replayVersion;
        try
        {
            replayVersion = ResolveReplayVersion(events, upToSequence);
        }
        catch (ArgumentException)
        {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.UnsupportedVersion,
                "Domain service version metadata is not supported for replay.",
                failedSequenceNumber: upToSequence);
        }

        DomainServiceRegistration? registration;
        try
        {
            registration = await resolver
                .ResolveAsync(identity.TenantId, identity.Domain, replayVersion, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DomainServiceException ex)
        {
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

        if (registration is null)
        {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.UnknownAggregateType,
                $"No domain service is registered for tenant '{identity.TenantId}', domain '{identity.Domain}'.");
        }

        // Build the replay wire payload. Replay envelopes carry only the metadata the Apply
        // path consumes plus diagnostics fields; sensitive fields (UserId) are intentionally
        // omitted so replay traffic does not duplicate identity information.
        ReplayEventEnvelope[] wireEvents = new ReplayEventEnvelope[events.Count];
        for (int i = 0; i < events.Count; i++)
        {
            EventEnvelope source = events[i];
            wireEvents[i] = new ReplayEventEnvelope(
                SequenceNumber: source.SequenceNumber,
                EventTypeName: source.EventTypeName,
                Payload: source.Payload,
                SerializationFormat: source.SerializationFormat,
                MetadataVersion: source.MetadataVersion,
                MessageId: source.MessageId,
                CorrelationId: string.IsNullOrWhiteSpace(source.CorrelationId) ? null : source.CorrelationId,
                CausationId: string.IsNullOrWhiteSpace(source.CausationId) ? null : source.CausationId);
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

        try
        {
            using HttpRequestMessage httpRequest = daprClient.CreateInvokeMethodRequest(
                registration.AppId,
                ReplayStateMethodName,
                request);
            HttpClient httpClient = httpClientFactory.CreateClient();
            using HttpResponseMessage httpResponse = await httpClient
                .SendAsync(httpRequest, cancellationToken)
                .ConfigureAwait(false);

            if (httpResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.UnknownAggregateType,
                    $"Domain service '{registration.AppId}' has no '{ReplayStateMethodName}' endpoint for tenant '{identity.TenantId}', domain '{identity.Domain}'.");
            }

            if (!httpResponse.IsSuccessStatusCode)
            {
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

            if (result is null)
            {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.Unexpected,
                    $"Replay endpoint returned an empty response (Tenant '{identity.TenantId}', Domain '{identity.Domain}').");
            }

            return result;
        }
        catch (JsonException ex)
        {
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
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

    private static string ResolveReplayVersion(IReadOnlyList<EventEnvelope> events, long upToSequence)
    {
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
