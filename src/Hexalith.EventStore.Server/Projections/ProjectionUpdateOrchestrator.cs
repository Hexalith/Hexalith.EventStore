using System.Net.Http.Json;
using System.Net;
using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Orchestrates projection updates: reads new events from the aggregate actor,
/// sends them to the domain service's /project endpoint via DAPR service invocation,
/// and stores the returned state in the EventReplayProjectionActor.
/// </summary>
/// <remarks>
/// Entire method is wrapped in try/catch — fire-and-forget safe. Any exception is
/// swallowed after logging. The projection stays at last known state on failure.
/// </remarks>
public partial class ProjectionUpdateOrchestrator(
    IActorProxyFactory actorProxyFactory,
    DaprClient daprClient,
    IHttpClientFactory httpClientFactory,
    IDomainServiceResolver resolver,
    IProjectionCheckpointTracker checkpointTracker,
    IOptions<ProjectionOptions> projectionOptions,
    ILogger<ProjectionUpdateOrchestrator> logger) : IProjectionUpdateOrchestrator, IProjectionPollerDeliveryGateway {
    // Per-aggregate serialization across orchestrator instances. Entries are evicted and the
    // underlying SemaphoreSlim disposed when the last holder releases, so a multi-tenant server
    // with many short-lived aggregates does not accumulate kernel handles indefinitely.
    internal static readonly KeyedSemaphore<string> ProjectionLocks = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public async Task UpdateProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);

        int refreshIntervalMs = projectionOptions.Value.GetRefreshIntervalMs(identity.Domain);
        if (refreshIntervalMs > 0) {
            try {
                await checkpointTracker.TrackIdentityAsync(identity, cancellationToken).ConfigureAwait(false);
                Log.PollingWorkRegistered(logger, identity.TenantId, identity.Domain, identity.AggregateId, refreshIntervalMs);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                Log.PollingWorkRegistrationFailed(logger, ex, identity.TenantId, identity.Domain, identity.AggregateId, ex.GetType().Name);
            }

            return;
        }

        await DeliverProjectionAsync(identity, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeliverProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);

        using IDisposable projectionLock = await ProjectionLocks.AcquireAsync(identity.ActorId, cancellationToken).ConfigureAwait(false);
        try {
            Log.UpdateStarted(logger, identity.TenantId, identity.Domain, identity.AggregateId);

            // Step 1: Resolve domain service registration
            DomainServiceRegistration? registration = await resolver
                .ResolveAsync(identity.TenantId, identity.Domain, "v1", cancellationToken)
                .ConfigureAwait(false);

            if (registration is null) {
                Log.NoDomainServiceRegistered(logger, identity.TenantId, identity.Domain);
                return;
            }

            // Step 2: Create aggregate actor proxy and read events
            IAggregateActor aggregateProxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId),
                "AggregateActor");

            // Full replay remains the safe immediate-delivery contract until
            // projection handlers receive prior state or become explicitly incremental-aware.
            EventEnvelope[] events = await aggregateProxy
                .GetEventsAsync(0)
                .ConfigureAwait(false);

            long lastDeliveredSequence = 0;
            try {
                lastDeliveredSequence = await checkpointTracker
                    .ReadLastDeliveredSequenceAsync(identity, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                Log.CheckpointReadFailed(logger, ex, identity.TenantId, identity.Domain, identity.AggregateId, ex.GetType().Name);
            }

            if (events.Length == 0) {
                // Drift detection covers the canonical "stale checkpoint + empty stream" case
                // (e.g., state-store backup/restore mismatch) that the original deferred-work
                // entry called out: without this branch the orchestrator would log NoEventsFound
                // and silently return, hiding the drift indefinitely.
                if (lastDeliveredSequence > 0) {
                    Log.CheckpointDriftDetected(
                        logger,
                        identity.TenantId,
                        identity.Domain,
                        identity.AggregateId,
                        ProjectionReasonCodes.CheckpointDrift,
                        lastDeliveredSequence,
                        0);
                    return;
                }

                Log.NoEventsFound(logger, identity.TenantId, identity.Domain, identity.AggregateId);
                return;
            }

            long highestAvailableSequence = events.Max(e => e.SequenceNumber);
            if (lastDeliveredSequence > highestAvailableSequence) {
                Log.CheckpointDriftDetected(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    ProjectionReasonCodes.CheckpointDrift,
                    lastDeliveredSequence,
                    highestAvailableSequence);
                return;
            }

            // Step 3: Map EventEnvelope[] to ProjectionEventDto[]
            var projectionEvents = new ProjectionEventDto[events.Length];
            for (int i = 0; i < events.Length; i++) {
                EventEnvelope e = events[i];
                projectionEvents[i] = new ProjectionEventDto(
                    e.EventTypeName,
                    e.Payload,
                    e.SerializationFormat,
                    e.SequenceNumber,
                    e.Timestamp,
                    e.CorrelationId);
            }

            // Step 4: Invoke domain service /project endpoint via DAPR
            var request = new ProjectionRequest(identity.TenantId, identity.Domain, identity.AggregateId, projectionEvents);
            using HttpRequestMessage httpRequest = daprClient.CreateInvokeMethodRequest(
                registration.AppId,
                "project",
                request);
            HttpClient httpClient = httpClientFactory.CreateClient();
            HttpResponseMessage? httpResponse = await SendProjectRequestAsync(
                    httpClient,
                    httpRequest,
                    registration.AppId,
                    identity,
                    cancellationToken)
                .ConfigureAwait(false);
            if (httpResponse is null) {
                return;
            }

            using HttpResponseMessage responseHandle = httpResponse;
            if (!responseHandle.IsSuccessStatusCode) {
                Log.ProjectInvocationRejected(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    registration.AppId,
                    GetUpstreamReasonCode(responseHandle.StatusCode),
                    ((int)responseHandle.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    GetContentTypeForLog(responseHandle.Content));
                return;
            }

            ProjectionResponse? response = await ReadProjectResponseAsync(
                    responseHandle,
                    registration.AppId,
                    identity,
                    cancellationToken)
                .ConfigureAwait(false);
            if (response is null) {
                return;
            }

            if (string.IsNullOrWhiteSpace(response.ProjectionType)) {
                Log.InvalidProjectionResponse(logger, identity.TenantId, identity.Domain, identity.AggregateId, ProjectionReasonCodes.ProjectInvalidProjectionType);
                return;
            }

            if (response.State.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                || (response.State.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(response.State.GetString()))) {
                Log.InvalidProjectionResponse(logger, identity.TenantId, identity.Domain, identity.AggregateId, ProjectionReasonCodes.ProjectInvalidState);
                return;
            }

            Log.DomainServiceInvocationSucceeded(logger, identity.TenantId, identity.Domain, identity.AggregateId, registration.AppId);

            // Step 5: Derive projection actor ID and update state
            string projectionActorId = QueryActorIdHelper.DeriveActorId(
                response.ProjectionType,
                identity.TenantId,
                identity.AggregateId,
                []);

            IProjectionWriteActor writeProxy = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(
                new ActorId(projectionActorId),
                QueryRouter.ProjectionActorTypeName);

            await writeProxy
                .UpdateProjectionAsync(ProjectionState.FromJsonElement(response.ProjectionType, identity.TenantId, response.State))
                .ConfigureAwait(false);

            long highestDeliveredSequence = highestAvailableSequence;
            try {
                bool checkpointSaved = await checkpointTracker
                    .SaveDeliveredSequenceAsync(identity, highestDeliveredSequence, cancellationToken)
                    .ConfigureAwait(false);
                if (!checkpointSaved) {
                    Log.CheckpointSaveExhausted(logger, identity.TenantId, identity.Domain, identity.AggregateId, highestDeliveredSequence);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                Log.CheckpointSaveFailed(logger, ex, identity.TenantId, identity.Domain, identity.AggregateId, ex.GetType().Name);
            }

            Log.ProjectionStateUpdated(logger, identity.TenantId, identity.Domain, identity.AggregateId, response.ProjectionType, projectionActorId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            Log.ProjectionUpdateFailed(logger, ex, identity.TenantId, identity.Domain, identity.AggregateId);
        }
    }

    private async Task<HttpResponseMessage?> SendProjectRequestAsync(
        HttpClient httpClient,
        HttpRequestMessage httpRequest,
        string appId,
        AggregateIdentity identity,
        CancellationToken cancellationToken) {
        try {
            return await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (TaskCanceledException ex) {
            Log.ProjectInvocationException(
                logger,
                ex,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                appId,
                ProjectionReasonCodes.ProjectTimeout,
                ex.GetType().Name,
                "0",
                "none");
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            Log.ProjectInvocationException(
                logger,
                ex,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                appId,
                ProjectionReasonCodes.Unknown,
                ex.GetType().Name,
                "0",
                "none");
            return null;
        }
    }

    private async Task<ProjectionResponse?> ReadProjectResponseAsync(
        HttpResponseMessage httpResponse,
        string appId,
        AggregateIdentity identity,
        CancellationToken cancellationToken) {
        string contentType = GetContentTypeForLog(httpResponse.Content);
        string httpStatus = ((int)httpResponse.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!IsJsonContent(httpResponse.Content)) {
            Log.ProjectInvocationRejected(
                logger,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                appId,
                ProjectionReasonCodes.ProjectUnsupportedContentType,
                httpStatus,
                contentType);
            return null;
        }

        string? charset = httpResponse.Content.Headers.ContentType?.CharSet;
        if (!string.IsNullOrWhiteSpace(charset)) {
            try {
                _ = Encoding.GetEncoding(charset.Trim('"'));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException) {
                _ = ex;
                Log.ProjectInvocationRejected(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    appId,
                    ProjectionReasonCodes.ProjectInvalidCharset,
                    httpStatus,
                    contentType);
                return null;
            }
        }

        try {
            return await httpResponse.Content
                .ReadFromJsonAsync<ProjectionResponse>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (JsonException ex) {
            Log.ProjectInvocationException(
                logger,
                ex,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                appId,
                ProjectionReasonCodes.ProjectMalformedJson,
                ex.GetType().Name,
                httpStatus,
                contentType);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            Log.ProjectInvocationException(
                logger,
                ex,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                appId,
                ProjectionReasonCodes.Unknown,
                ex.GetType().Name,
                httpStatus,
                contentType);
            return null;
        }
    }

    private static bool IsJsonContent(HttpContent content) {
        string? mediaType = content.Headers.ContentType?.MediaType;
        return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
            || (mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string GetContentTypeForLog(HttpContent? content) =>
        content?.Headers.ContentType?.ToString() ?? "none";

    private static string GetUpstreamReasonCode(HttpStatusCode statusCode) =>
        (int)statusCode >= 400 && (int)statusCode <= 499
            ? ProjectionReasonCodes.ProjectUpstream4xx
            : (int)statusCode >= 500 && (int)statusCode <= 599
                ? ProjectionReasonCodes.ProjectUpstream5xx
                : ProjectionReasonCodes.ProjectUnexpectedStatus;

    private static partial class Log {
        [LoggerMessage(
            EventId = 1110,
            Level = LogLevel.Debug,
            Message = "Projection update started: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=ProjectionUpdateStarted")]
        public static partial void UpdateStarted(ILogger logger, string tenantId, string domain, string aggregateId);

        [LoggerMessage(
            EventId = 1111,
            Level = LogLevel.Information,
            Message = "No domain service registered for projection update: TenantId={TenantId}, Domain={Domain}, Stage=NoDomainServiceRegistered")]
        public static partial void NoDomainServiceRegistered(ILogger logger, string tenantId, string domain);

        [LoggerMessage(
            EventId = 1112,
            Level = LogLevel.Debug,
            Message = "No events found for projection update: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=NoEventsFound")]
        public static partial void NoEventsFound(ILogger logger, string tenantId, string domain, string aggregateId);

        [LoggerMessage(
            EventId = 1113,
            Level = LogLevel.Debug,
            Message = "Domain service invocation succeeded for projection: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, AppId={AppId}, Stage=DomainServiceInvocationSucceeded")]
        public static partial void DomainServiceInvocationSucceeded(ILogger logger, string tenantId, string domain, string aggregateId, string appId);

        [LoggerMessage(
            EventId = 1114,
            Level = LogLevel.Debug,
            Message = "Projection state updated: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ProjectionType={ProjectionType}, ActorId={ActorId}, Stage=ProjectionStateUpdated")]
        public static partial void ProjectionStateUpdated(ILogger logger, string tenantId, string domain, string aggregateId, string projectionType, string actorId);

        [LoggerMessage(
            EventId = 1115,
            Level = LogLevel.Warning,
            Message = "Projection update failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=ProjectionUpdateFailed")]
        public static partial void ProjectionUpdateFailed(ILogger logger, Exception ex, string tenantId, string domain, string aggregateId);

        [LoggerMessage(
            EventId = 1116,
            Level = LogLevel.Warning,
            Message = "Invalid projection response: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ReasonCode={ReasonCode}, Stage=InvalidProjectionResponse")]
        public static partial void InvalidProjectionResponse(ILogger logger, string tenantId, string domain, string aggregateId, string reasonCode);

        [LoggerMessage(
            EventId = 1117,
            Level = LogLevel.Debug,
            Message = "Projection polling work registered: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, RefreshIntervalMs={RefreshIntervalMs}, Stage=ProjectionPollingWorkRegistered")]
        public static partial void PollingWorkRegistered(ILogger logger, string tenantId, string domain, string aggregateId, int refreshIntervalMs);

        [LoggerMessage(
            EventId = 1121,
            Level = LogLevel.Warning,
            Message = "Projection polling work registration failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ExceptionType={ExceptionType}, Stage=ProjectionPollingWorkRegistrationFailed")]
        public static partial void PollingWorkRegistrationFailed(ILogger logger, Exception ex, string tenantId, string domain, string aggregateId, string exceptionType);

        [LoggerMessage(
            EventId = 1118,
            Level = LogLevel.Warning,
            Message = "Projection checkpoint read failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ExceptionType={ExceptionType}, Stage=ProjectionCheckpointReadFailed")]
        public static partial void CheckpointReadFailed(ILogger logger, Exception ex, string tenantId, string domain, string aggregateId, string exceptionType);

        [LoggerMessage(
            EventId = 1119,
            Level = LogLevel.Warning,
            Message = "Projection checkpoint save failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ExceptionType={ExceptionType}, Stage=ProjectionCheckpointSaveFailed")]
        public static partial void CheckpointSaveFailed(ILogger logger, Exception ex, string tenantId, string domain, string aggregateId, string exceptionType);

        [LoggerMessage(
            EventId = 1120,
            Level = LogLevel.Warning,
            Message = "Projection checkpoint save exhausted optimistic-concurrency attempts: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, AttemptedSequence={AttemptedSequence}, Stage=ProjectionCheckpointSaveExhausted")]
        public static partial void CheckpointSaveExhausted(ILogger logger, string tenantId, string domain, string aggregateId, long attemptedSequence);

        [LoggerMessage(
            EventId = 1141,
            Level = LogLevel.Warning,
            Message = "Projection /project response rejected: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, AppId={AppId}, ReasonCode={ReasonCode}, HttpStatus={HttpStatus}, ContentType={ContentType}, Stage=ProjectInvocationRejected")]
        public static partial void ProjectInvocationRejected(ILogger logger, string tenantId, string domain, string aggregateId, string appId, string reasonCode, string httpStatus, string contentType);

        [LoggerMessage(
            EventId = 1142,
            Level = LogLevel.Warning,
            Message = "Projection /project invocation failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, AppId={AppId}, ReasonCode={ReasonCode}, ExceptionType={ExceptionType}, HttpStatus={HttpStatus}, ContentType={ContentType}, Stage=ProjectInvocationFailed")]
        public static partial void ProjectInvocationException(ILogger logger, Exception ex, string tenantId, string domain, string aggregateId, string appId, string reasonCode, string exceptionType, string httpStatus, string contentType);

        [LoggerMessage(
            EventId = 1143,
            Level = LogLevel.Warning,
            Message = "Projection checkpoint drift detected: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ReasonCode={ReasonCode}, LastDeliveredSequence={LastDeliveredSequence}, HighestEventSequence={HighestEventSequence}, Stage=ProjectionCheckpointDrift")]
        public static partial void CheckpointDriftDetected(ILogger logger, string tenantId, string domain, string aggregateId, string reasonCode, long lastDeliveredSequence, long highestEventSequence);
    }
}
