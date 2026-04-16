using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Controllers;

/// <summary>
/// Handles admin trace query endpoints for correlation ID trace map computation.
/// Operates on a different URL namespace (/traces/{tenantId}/{correlationId}) because
/// the trace map takes a correlation ID as primary identifier, not a stream identity.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/admin/traces")]
[Tags("Admin - Trace Queries")]
public class AdminTraceQueryController(
    ICommandStatusStore commandStatusStore,
    IActorProxyFactory actorProxyFactory,
    IConfiguration configuration,
    ILogger<AdminTraceQueryController> logger) : ControllerBase {
    private const int MaxEventScan = 10_000;

    /// <summary>
    /// Computes the correlation trace map for a given correlation ID.
    /// Reads command status, scans aggregate events, determines projection status, and builds external trace URL.
    /// </summary>
    [HttpGet("{tenantId}/{correlationId}")]
    [ProducesResponseType(typeof(CorrelationTraceMap), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCorrelationTraceMap(
        string tenantId,
        string correlationId,
        [FromQuery] string? domain,
        [FromQuery] string? aggregateId,
        CancellationToken ct = default) {
        if (string.IsNullOrWhiteSpace(correlationId)) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "correlationId is required.");
        }

        try {
            // Step 1: Read command status
            CommandStatusRecord? commandStatus = await commandStatusStore
                .ReadStatusAsync(tenantId, correlationId, ct)
                .ConfigureAwait(false);

            string resolvedDomain;
            string resolvedAggregateId;
            string commandType = string.Empty;
            string commandStatusStr = "Unknown";
            string? userId = null;
            DateTimeOffset? commandReceivedAt = null;
            DateTimeOffset? commandCompletedAt = null;
            long? durationMs = null;
            int? expectedEventCount = null;
            string? rejectionEventType = null;
            string? errorMessage = null;

            if (commandStatus is not null) {
                commandStatusStr = commandStatus.Status.ToString();
                commandReceivedAt = commandStatus.Timestamp;
                resolvedDomain = domain ?? string.Empty;
                resolvedAggregateId = commandStatus.AggregateId ?? aggregateId ?? string.Empty;

                if (IsTerminalStatus(commandStatus.Status)) {
                    commandCompletedAt = commandStatus.Timestamp;
                }

                expectedEventCount = commandStatus.EventCount;
                rejectionEventType = commandStatus.RejectionEventType;

                if (commandStatus.Status == CommandStatus.PublishFailed) {
                    errorMessage = commandStatus.FailureReason ?? "Publication failed.";
                }
                else if (commandStatus.Status == CommandStatus.TimedOut) {
                    errorMessage = commandStatus.TimeoutDuration.HasValue
                        ? $"Command timed out after {commandStatus.TimeoutDuration.Value.TotalSeconds:F0}s."
                        : "Command timed out.";
                }
            }
            else if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(aggregateId)) {
                // Events-first fallback: command status expired but stream context provided
                resolvedDomain = domain;
                resolvedAggregateId = aggregateId;
                errorMessage = "Command status not found \u2014 the status record may have expired (default 24-hour TTL). Events were found by scanning the aggregate stream.";
            }
            else {
                // No command status and no stream context — return Unknown
                string? externalTraceUrl = BuildExternalTraceUrl(correlationId);
                return Ok(new CorrelationTraceMap(
                    CorrelationId: correlationId,
                    TenantId: tenantId,
                    Domain: string.Empty,
                    AggregateId: string.Empty,
                    CommandType: string.Empty,
                    CommandStatus: "Unknown",
                    UserId: null,
                    CommandReceivedAt: null,
                    CommandCompletedAt: null,
                    DurationMs: null,
                    ProducedEvents: [],
                    AffectedProjections: [],
                    RejectionEventType: null,
                    ErrorMessage: "Command status not found \u2014 the status record may have expired (default 24-hour TTL) or the correlation ID is invalid. Try opening the trace from a stream view where the aggregate context is known.",
                    ExternalTraceUrl: externalTraceUrl,
                    TotalStreamEvents: 0,
                    ScanCapped: false,
                    ScanCapMessage: null));
            }

            // Step 2: Read events from aggregate stream
            List<TraceMapEvent> producedEvents = [];
            long totalStreamEvents = 0;
            bool scanCapped = false;
            string? scanCapMessage = null;

            if (!string.IsNullOrEmpty(resolvedDomain) && !string.IsNullOrEmpty(resolvedAggregateId)) {
                try {
                    var identity = new AggregateIdentity(tenantId, resolvedDomain, resolvedAggregateId);
                    IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                        new ActorId(identity.ActorId), "AggregateActor");

                    ServerEventEnvelope[] allEvents = await actor.GetEventsAsync(0).ConfigureAwait(false);
                    totalStreamEvents = allEvents.Length;

                    if (allEvents.Length > 0) {
                        // Scan backward from latest, cap at MaxEventScan
                        int scanStart = Math.Max(0, allEvents.Length - MaxEventScan);

                        for (int i = allEvents.Length - 1; i >= scanStart; i--) {
                            ServerEventEnvelope evt = allEvents[i];
                            if (string.Equals(evt.CorrelationId, correlationId, StringComparison.Ordinal)) {
                                producedEvents.Add(new TraceMapEvent(
                                    SequenceNumber: evt.SequenceNumber,
                                    EventTypeName: evt.EventTypeName ?? string.Empty,
                                    Timestamp: evt.Timestamp,
                                    CausationId: evt.CausationId,
                                    IsRejection: IsRejectionEvent(evt)));

                                // If we found all expected events, stop early
                                if (expectedEventCount.HasValue && producedEvents.Count >= expectedEventCount.Value) {
                                    break;
                                }
                            }
                        }

                        // Sort by sequence number ascending
                        producedEvents.Sort((a, b) => a.SequenceNumber.CompareTo(b.SequenceNumber));

                        // Check if scan was capped
                        if (scanStart > 0 && expectedEventCount.HasValue && producedEvents.Count < expectedEventCount.Value) {
                            scanCapped = true;
                            scanCapMessage = $"Event scan was limited to the most recent {MaxEventScan:N0} events. Older events for this correlation may exist but were not included. The command produced {expectedEventCount.Value} events but only {producedEvents.Count} were found within the scan window.";
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException) {
                    logger.LogWarning(ex, "Failed to read events for trace map {TenantId}/{Domain}/{AggregateId}.",
                        tenantId, resolvedDomain, resolvedAggregateId);
                    // Continue with empty events — partial result is better than failure
                }
            }

            // Step 3: Determine affected projections (graceful degradation — no projection query in EventStore)
            List<TraceMapProjection> affectedProjections = [];

            // Step 4: Build external trace URL
            string? traceUrl = BuildExternalTraceUrl(correlationId);

            // Step 5: Compute timing
            if (commandReceivedAt.HasValue && commandCompletedAt.HasValue) {
                durationMs = (long)(commandCompletedAt.Value - commandReceivedAt.Value).TotalMilliseconds;
            }

            // Step 6: Return CorrelationTraceMap
            CorrelationTraceMap result = new(
                CorrelationId: correlationId,
                TenantId: tenantId,
                Domain: resolvedDomain,
                AggregateId: resolvedAggregateId,
                CommandType: commandType,
                CommandStatus: commandStatusStr,
                UserId: userId,
                CommandReceivedAt: commandReceivedAt,
                CommandCompletedAt: commandCompletedAt,
                DurationMs: durationMs,
                ProducedEvents: producedEvents,
                AffectedProjections: affectedProjections,
                RejectionEventType: rejectionEventType,
                ErrorMessage: errorMessage,
                ExternalTraceUrl: traceUrl,
                TotalStreamEvents: totalStreamEvents,
                ScanCapped: scanCapped,
                ScanCapMessage: scanCapMessage);

            return Ok(result);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to compute trace map for {TenantId}/{CorrelationId}.",
                tenantId, correlationId);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "Failed to compute correlation trace map.");
        }
    }

    private static bool IsTerminalStatus(CommandStatus status)
        => status is CommandStatus.Completed
            or CommandStatus.Rejected
            or CommandStatus.PublishFailed
            or CommandStatus.TimedOut;

    private static bool IsRejectionEvent(ServerEventEnvelope evt) {
        // Convention: rejection events have type names ending with "Rejected" or containing "Rejection"
        string typeName = evt.EventTypeName ?? string.Empty;
        return typeName.EndsWith("Rejected", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("Rejection", StringComparison.OrdinalIgnoreCase);
    }

    private string? BuildExternalTraceUrl(string correlationId) {
        string? adminTraceUrl = configuration["ADMIN_TRACE_URL"];
        if (string.IsNullOrEmpty(adminTraceUrl)) {
            return null;
        }

        return adminTraceUrl.Contains("{correlationId}", StringComparison.OrdinalIgnoreCase)
            ? adminTraceUrl.Replace("{correlationId}", Uri.EscapeDataString(correlationId), StringComparison.OrdinalIgnoreCase)
            : $"{adminTraceUrl}?correlationId={Uri.EscapeDataString(correlationId)}";
    }
}
