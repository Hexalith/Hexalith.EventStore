using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Controllers;

/// <summary>
/// Handles admin stream query endpoints that require direct actor access for computation.
/// This controller provides the computation layer for admin stream queries;
/// the Admin.Server AdminStreamsController provides the REST facade.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/admin/streams")]
[Tags("Admin - Stream Queries")]
public class AdminStreamQueryController(
    IActorProxyFactory actorProxyFactory,
    IDomainServiceInvoker domainServiceInvoker,
    ILogger<AdminStreamQueryController> logger) : ControllerBase {
    private const int _defaultMaxBisectFields = 1_000;
    private const int _defaultMaxBisectSteps = 30;
    private const int _defaultMaxEvents = 10_000;
    private const int _defaultMaxFields = 5_000;

    /// <summary>
    /// Performs a binary search through event history to find the exact event where aggregate state
    /// diverged from expected field values. Reconstructs state at O(log N) midpoints for comparison.
    /// </summary>
    [HttpGet("{tenantId}/{domain}/{aggregateId}/bisect")]
    [ProducesResponseType(typeof(BisectResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BisectAggregateStateAsync(
        string tenantId,
        string domain,
        string aggregateId,
        [FromQuery] long good,
        [FromQuery] long bad,
        [FromQuery] string? fields,
        [FromQuery] int maxSteps = _defaultMaxBisectSteps,
        [FromQuery] int maxFields = _defaultMaxBisectFields,
        CancellationToken ct = default) {
        if (good < 0) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Parameter 'good' must be >= 0.");
        }

        if (bad <= 0) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Parameter 'bad' must be > 0.");
        }

        if (good >= bad) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Parameter 'good' must be less than 'bad'.");
        }

        if (maxSteps <= 0) {
            maxSteps = _defaultMaxBisectSteps;
        }

        if (maxFields <= 0) {
            maxFields = _defaultMaxBisectFields;
        }

        // Parse comma-separated field paths
        IReadOnlyList<string> fieldPaths = string.IsNullOrWhiteSpace(fields)
            ? []
            : fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        try {
            var identity = new AggregateIdentity(tenantId, domain, aggregateId);
            IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId), "AggregateActor");

            ServerEventEnvelope[] allEvents = await actor.GetEventsAsync(0).ConfigureAwait(false);

            if (allEvents.Length == 0) {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: "Stream not found or has no events.");
            }

            long actualMaxSequence = allEvents[^1].SequenceNumber;
            if (good > actualMaxSequence) {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request",
                    detail: $"Parameter 'good' ({good}) exceeds stream length ({actualMaxSequence}).");
            }

            if (bad > actualMaxSequence) {
                bad = actualMaxSequence;
            }

            // Reconstruct state at the good sequence to establish expected field values
            JsonObject goodState = ReconstructState(allEvents, good);
            Dictionary<string, string> allLeafFields = FlattenJson(goodState, string.Empty);

            // If good state is empty (e.g., good=0), use bad state's fields as the watch set
            // to avoid vacuous comparisons where all midpoints report "good"
            if (allLeafFields.Count == 0 && fieldPaths.Count == 0) {
                JsonObject badState = ReconstructState(allEvents, bad);
                allLeafFields = FlattenJson(badState, string.Empty);
            }

            // Determine watched fields
            List<string> watchedFieldPaths;
            if (fieldPaths.Count > 0) {
                watchedFieldPaths = fieldPaths.ToList();
            }
            else {
                // All leaf fields
                if (allLeafFields.Count > maxFields) {
                    return Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: $"State has {allLeafFields.Count} fields — specify field paths to narrow the comparison (max {maxFields} fields).");
                }

                watchedFieldPaths = [.. allLeafFields.Keys];
            }

            // Extract expected values from good state
            Dictionary<string, JsonElement> expectedValues = ExtractFieldValues(goodState, watchedFieldPaths);

            // Binary search
            long goodSeq = good;
            long badSeq = bad;
            var steps = new List<BisectStep>();
            int step = 0;

            while (badSeq - goodSeq > 1 && step < maxSteps) {
                ct.ThrowIfCancellationRequested();

                step++;
                long mid = goodSeq + ((badSeq - goodSeq) / 2);

                // Reconstruct state at midpoint
                JsonObject midState = ReconstructState(allEvents, mid);
                Dictionary<string, JsonElement> midValues = ExtractFieldValues(midState, watchedFieldPaths);

                // Compare field values using JsonElement.DeepEquals
                int divergentCount = CountDivergentFields(midValues, expectedValues);

                if (divergentCount == 0) {
                    steps.Add(new BisectStep(step, mid, "good", 0));
                    goodSeq = mid;
                }
                else {
                    steps.Add(new BisectStep(step, mid, "bad", divergentCount));
                    badSeq = mid;
                }
            }

            bool isTruncated = badSeq - goodSeq > 1;

            // Get divergent event metadata and field changes
            ServerEventEnvelope? divergentEvent = allEvents.FirstOrDefault(e => e.SequenceNumber == badSeq);
            List<FieldChange> divergentFieldChanges = [];

            if (divergentEvent is not null) {
                // Diff state at badSeq-1 vs badSeq to get the exact field changes at the divergent event
                JsonObject stateBeforeDivergent = ReconstructState(allEvents, badSeq - 1);
                JsonObject stateAtDivergent = ReconstructState(allEvents, badSeq);

                Dictionary<string, (string NewValue, string OldValue)> allChanges = JsonDiff(
                    stateBeforeDivergent,
                    stateAtDivergent,
                    string.Empty);

                // Filter to only watched fields
                foreach (KeyValuePair<string, (string NewValue, string OldValue)> change in allChanges) {
                    if (watchedFieldPaths.Count == 0 || watchedFieldPaths.Contains(change.Key, StringComparer.Ordinal)) {
                        divergentFieldChanges.Add(new FieldChange(
                            change.Key,
                            change.Value.OldValue,
                            change.Value.NewValue));
                    }
                }
            }

            BisectResult result = new(
                TenantId: tenantId,
                Domain: domain,
                AggregateId: aggregateId,
                GoodSequence: goodSeq,
                DivergentSequence: badSeq,
                DivergentTimestamp: divergentEvent?.Timestamp ?? DateTimeOffset.MinValue,
                DivergentEventType: divergentEvent?.EventTypeName ?? string.Empty,
                DivergentCorrelationId: divergentEvent?.CorrelationId ?? string.Empty,
                DivergentUserId: divergentEvent?.UserId ?? string.Empty,
                DivergentFieldChanges: divergentFieldChanges,
                WatchedFieldPaths: watchedFieldPaths,
                Steps: steps,
                TotalSteps: step,
                IsTruncated: isTruncated);

            return Ok(result);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to bisect aggregate state for {TenantId}/{Domain}/{AggregateId} (good={Good}, bad={Bad}).",
                tenantId, domain, aggregateId, good, bad);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "Failed to compute bisect result.");
        }
    }

    /// <summary>
    /// Computes per-field blame (provenance) for an aggregate's state at a given sequence position.
    /// Uses an incremental O(N) algorithm: replay events, diff state after each apply, track blame.
    /// </summary>
    [HttpGet("{tenantId}/{domain}/{aggregateId}/blame")]
    [ProducesResponseType(typeof(AggregateBlameView), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAggregateBlameAsync(
        string tenantId,
        string domain,
        string aggregateId,
        [FromQuery] long? at,
        [FromQuery] int maxEvents = _defaultMaxEvents,
        [FromQuery] int maxFields = _defaultMaxFields,
        CancellationToken _ = default) {
        if (at.HasValue && at.Value < 1) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Parameter 'at' must be >= 1 when provided.");
        }

        if (maxEvents <= 0) {
            maxEvents = _defaultMaxEvents;
        }

        if (maxFields <= 0) {
            maxFields = _defaultMaxFields;
        }

        try {
            var identity = new AggregateIdentity(tenantId, domain, aggregateId);
            IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId), "AggregateActor");

            ServerEventEnvelope[] allEvents = await actor.GetEventsAsync(0).ConfigureAwait(false);

            if (allEvents.Length == 0) {
                return Ok(new AggregateBlameView(
                    tenantId, domain, aggregateId,
                    AtSequence: 0,
                    Timestamp: DateTimeOffset.MinValue,
                    Fields: [],
                    IsTruncated: false,
                    IsFieldsTruncated: false));
            }

            long actualMaxSequence = allEvents[^1].SequenceNumber;
            long atSequence = Math.Min(at ?? actualMaxSequence, actualMaxSequence);

            // Filter events up to atSequence
            ServerEventEnvelope[] eventsInRange = allEvents
                .Where(e => e.SequenceNumber <= atSequence)
                .OrderBy(e => e.SequenceNumber)
                .ToArray();

            if (eventsInRange.Length == 0) {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: "No events found at or before the specified sequence.");
            }

            // Truncation: if more events than maxEvents, start from a later position
            bool isTruncated = eventsInRange.Length > maxEvents;
            long startSequence = 1;
            if (isTruncated) {
                startSequence = atSequence - maxEvents + 1;
                if (startSequence < 1) {
                    startSequence = 1;
                }

                eventsInRange = eventsInRange
                    .Where(e => e.SequenceNumber >= startSequence)
                    .ToArray();
            }

            // Incremental O(N) blame algorithm using JSON-level state tracking
            AggregateBlameView result = ComputeBlame(
                tenantId, domain, aggregateId,
                eventsInRange, atSequence, isTruncated, maxFields);

            return Ok(result);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to compute blame for {TenantId}/{Domain}/{AggregateId} at {AtSequence}.",
                tenantId, domain, aggregateId, at);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "Failed to compute blame view.");
        }
    }

    /// <summary>
    /// Computes a single step-through debugging frame for an aggregate's event history.
    /// Returns event metadata, aggregate state at the specified sequence, and field changes from the previous sequence.
    /// </summary>
    [HttpGet("{tenantId}/{domain}/{aggregateId}/step")]
    [ProducesResponseType(typeof(EventStepFrame), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventStepFrameAsync(
        string tenantId,
        string domain,
        string aggregateId,
        [FromQuery] long at,
        CancellationToken ct = default) {
        if (at < 1) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Parameter 'at' must be >= 1.");
        }

        try {
            var identity = new AggregateIdentity(tenantId, domain, aggregateId);
            IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId), "AggregateActor");

            ServerEventEnvelope[] allEvents = await actor.GetEventsAsync(0).ConfigureAwait(false);

            if (allEvents.Length == 0) {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: "Stream not found or has no events.");
            }

            long totalEvents = allEvents[^1].SequenceNumber;
            if (at > totalEvents) {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request",
                    detail: $"Sequence {at} is beyond the stream's {totalEvents} events.");
            }

            ct.ThrowIfCancellationRequested();

            // Find the event at the requested sequence
            ServerEventEnvelope? targetEvent = allEvents.FirstOrDefault(e => e.SequenceNumber == at);
            if (targetEvent is null) {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Event at sequence {at} not found.");
            }

            // Single-pass optimization: reconstruct state at N and capture state at N-1 during the same replay
            JsonObject stateAtPrev = [];
            JsonObject stateAtCurrent = [];
            foreach (ServerEventEnvelope evt in allEvents) {
                if (evt.SequenceNumber > at) {
                    break;
                }

                ct.ThrowIfCancellationRequested();

                if (evt.SequenceNumber == at) {
                    // Capture state at N-1 before applying the last event
                    stateAtPrev = (JsonObject?)JsonNode.Parse(stateAtCurrent.ToJsonString()) ?? [];
                }

                try {
                    var eventPayload = JsonNode.Parse(evt.Payload);
                    if (eventPayload is JsonObject payloadObj) {
                        DeepMerge(stateAtCurrent, payloadObj);
                    }
                }
                catch (JsonException) {
                    continue;
                }
            }

            string stateJson = stateAtCurrent.ToJsonString();

            // Compute field changes between previous state and current state
            Dictionary<string, (string NewValue, string OldValue)> changes = JsonDiff(stateAtPrev, stateAtCurrent, string.Empty);
            var fieldChanges = changes
                .Select(c => new FieldChange(c.Key, c.Value.OldValue, c.Value.NewValue))
                .OrderBy(fc => fc.FieldPath, StringComparer.Ordinal)
                .ToList();

            // Get event payload as JSON string
            string eventPayloadJson;
            try {
                eventPayloadJson = System.Text.Encoding.UTF8.GetString(targetEvent.Payload);
            }
            catch {
                eventPayloadJson = "{}";
            }

            EventStepFrame frame = new(
                TenantId: tenantId,
                Domain: domain,
                AggregateId: aggregateId,
                SequenceNumber: at,
                EventTypeName: targetEvent.EventTypeName ?? string.Empty,
                Timestamp: targetEvent.Timestamp,
                CorrelationId: targetEvent.CorrelationId ?? string.Empty,
                CausationId: targetEvent.CausationId ?? string.Empty,
                UserId: targetEvent.UserId ?? string.Empty,
                EventPayloadJson: eventPayloadJson,
                StateJson: stateJson,
                FieldChanges: fieldChanges,
                TotalEvents: totalEvents);

            return Ok(frame);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to compute step frame for {TenantId}/{Domain}/{AggregateId} at {At}.",
                tenantId, domain, aggregateId, at);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "Failed to compute event step frame.");
        }
    }

    /// <summary>
    /// Executes a command in sandbox (dry-run) mode against reconstructed aggregate state.
    /// Invokes the domain service Handle method via DAPR but does NOT persist any events.
    /// Returns the events that would be produced, the resulting state, and a state diff.
    /// </summary>
    [HttpPost("{tenantId}/{domain}/{aggregateId}/sandbox")]
    [ProducesResponseType(typeof(SandboxResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SandboxCommandAsync(
        string tenantId,
        string domain,
        string aggregateId,
        [FromBody] SandboxCommandRequest request,
        CancellationToken ct = default) {
        if (request is null) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CommandType)) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "CommandType is required.");
        }

        // Default empty payload to "{}"
        string payloadJson = string.IsNullOrEmpty(request.PayloadJson) ? "{}" : request.PayloadJson;

        // Validate payload is valid JSON
        try {
            using var doc = JsonDocument.Parse(payloadJson);
        }
        catch (JsonException) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "PayloadJson is not valid JSON.");
        }

        if (request.AtSequence.HasValue && request.AtSequence.Value < 0) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "AtSequence must be >= 0 when provided.");
        }

        long sw = Stopwatch.GetTimestamp();

        try {
            // Step 2: Reconstruct state
            ServerEventEnvelope[] allEvents;
            JsonObject inputState;
            long atSequence;

            if (request.AtSequence == 0) {
                // Empty initial state — no stream lookup needed
                allEvents = [];
                inputState = [];
                atSequence = 0;
            }
            else {
                var identity = new AggregateIdentity(tenantId, domain, aggregateId);
                IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                    new ActorId(identity.ActorId), "AggregateActor");

                allEvents = await actor.GetEventsAsync(0).ConfigureAwait(false);

                if (allEvents.Length == 0) {
                    return Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Not Found",
                        detail: "Stream not found or has no events.");
                }

                long actualMaxSequence = allEvents[^1].SequenceNumber;

                if (request.AtSequence.HasValue) {
                    atSequence = request.AtSequence.Value;
                    if (atSequence > actualMaxSequence) {
                        return Problem(
                            statusCode: StatusCodes.Status400BadRequest,
                            title: "Bad Request",
                            detail: $"AtSequence ({atSequence}) exceeds stream length ({actualMaxSequence}).");
                    }
                }
                else {
                    atSequence = actualMaxSequence;
                }

                inputState = ReconstructState(allEvents, atSequence);
            }

            ct.ThrowIfCancellationRequested();

            // Step 3: Invoke domain service
            CommandEnvelope commandEnvelope = new(
                MessageId: Guid.NewGuid().ToString(),
                TenantId: tenantId,
                Domain: domain,
                AggregateId: aggregateId,
                CommandType: request.CommandType,
                Payload: Encoding.UTF8.GetBytes(payloadJson),
                CorrelationId: string.IsNullOrEmpty(request.CorrelationId) ? Guid.NewGuid().ToString() : request.CorrelationId,
                CausationId: null,
                UserId: string.IsNullOrEmpty(request.UserId) ? "sandbox-user" : request.UserId,
                Extensions: null);

            DomainResult domainResult;
            try {
                object? currentState = atSequence == 0 ? null : inputState;
                domainResult = await domainServiceInvoker.InvokeAsync(commandEnvelope, currentState, ct).ConfigureAwait(false);
            }
            catch (DomainServiceNotFoundException ex) {
                long elapsedError = (long)Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
                return Ok(new SandboxResult(
                    TenantId: tenantId,
                    Domain: domain,
                    AggregateId: aggregateId,
                    AtSequence: atSequence,
                    CommandType: request.CommandType,
                    Outcome: "error",
                    ProducedEvents: [],
                    ResultingStateJson: string.Empty,
                    StateChanges: [],
                    ErrorMessage: $"No domain service registered for domain '{domain}' in tenant '{tenantId}'. {ex.Message}",
                    ExecutionTimeMs: elapsedError));
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                long elapsedError = (long)Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
                return Ok(new SandboxResult(
                    TenantId: tenantId,
                    Domain: domain,
                    AggregateId: aggregateId,
                    AtSequence: atSequence,
                    CommandType: request.CommandType,
                    Outcome: "error",
                    ProducedEvents: [],
                    ResultingStateJson: string.Empty,
                    StateChanges: [],
                    ErrorMessage: $"Domain service invocation failed: {ex.Message}",
                    ExecutionTimeMs: elapsedError));
            }

            // Step 4 & 5: Parse result and compute resulting state + diff
            string outcome;
            List<SandboxEvent> producedEvents = [];
            string resultingStateJson = string.Empty;
            List<FieldChange> stateChanges = [];

            if (domainResult.IsRejection) {
                outcome = "rejected";
                producedEvents = ExtractSandboxEvents(domainResult.Events);
            }
            else if (domainResult.IsNoOp) {
                outcome = "accepted";
                resultingStateJson = inputState.ToJsonString();
            }
            else {
                // Success — apply events and compute diff
                outcome = "accepted";
                producedEvents = ExtractSandboxEvents(domainResult.Events);

                // Apply produced events to input state to compute resulting state
                JsonObject resultingState = (JsonObject?)JsonNode.Parse(inputState.ToJsonString()) ?? [];
                foreach (SandboxEvent sandboxEvent in producedEvents) {
                    try {
                        var eventNode = JsonNode.Parse(sandboxEvent.PayloadJson);
                        if (eventNode is JsonObject eventObj) {
                            DeepMerge(resultingState, eventObj);
                        }
                    }
                    catch (JsonException) {
                        // Skip malformed event payloads
                        continue;
                    }
                }

                resultingStateJson = resultingState.ToJsonString();

                // Compute state diff
                Dictionary<string, (string NewValue, string OldValue)> changes = JsonDiff(inputState, resultingState, string.Empty);
                stateChanges = changes
                    .Select(c => new FieldChange(c.Key, c.Value.OldValue, c.Value.NewValue))
                    .OrderBy(fc => fc.FieldPath, StringComparer.Ordinal)
                    .ToList();
            }

            long elapsedMs = (long)Stopwatch.GetElapsedTime(sw).TotalMilliseconds;

            SandboxResult result = new(
                TenantId: tenantId,
                Domain: domain,
                AggregateId: aggregateId,
                AtSequence: atSequence,
                CommandType: request.CommandType,
                Outcome: outcome,
                ProducedEvents: producedEvents,
                ResultingStateJson: resultingStateJson,
                StateChanges: stateChanges,
                ErrorMessage: null,
                ExecutionTimeMs: elapsedMs);

            return Ok(result);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to execute sandbox command for {TenantId}/{Domain}/{AggregateId}.",
                tenantId, domain, aggregateId);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "Failed to execute sandbox command.");
        }
    }

    /// <summary>
    /// Implements the incremental O(N) blame algorithm.
    /// Maintains a running JSON state and diffs after each event to track which event
    /// last changed each field.
    /// </summary>
    private static AggregateBlameView ComputeBlame(
        string tenantId,
        string domain,
        string aggregateId,
        ServerEventEnvelope[] events,
        long atSequence,
        bool isTruncated,
        int maxFields) {
        // blame_map: fieldPath -> FieldProvenance
        var blameMap = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal);
        var previousState = JsonNode.Parse("{}");
        var currentState = JsonNode.Parse("{}");
        DateTimeOffset lastTimestamp = DateTimeOffset.MinValue;

        foreach (ServerEventEnvelope evt in events) {
            previousState = currentState?.DeepClone();

            // Apply event payload to state using JSON merge
            try {
                var eventPayload = JsonNode.Parse(evt.Payload);
                if (eventPayload is JsonObject payloadObj && currentState is JsonObject stateObj) {
                    DeepMerge(stateObj, payloadObj);
                }
            }
            catch (JsonException) {
                // Event payload is not valid JSON (e.g., binary format) — skip this event
                continue;
            }

            // Diff previous and current state to find changed fields
            Dictionary<string, (string NewValue, string OldValue)> changes = JsonDiff(
                previousState as JsonObject,
                currentState as JsonObject,
                prefix: string.Empty);

            foreach (KeyValuePair<string, (string NewValue, string OldValue)> change in changes) {
                blameMap[change.Key] = new FieldProvenance(
                    FieldPath: change.Key,
                    CurrentValue: change.Value.NewValue,
                    PreviousValue: change.Value.OldValue,
                    LastChangedAtSequence: evt.SequenceNumber,
                    LastChangedAtTimestamp: evt.Timestamp,
                    LastChangedByEventType: evt.EventTypeName,
                    LastChangedByCorrelationId: evt.CorrelationId ?? string.Empty,
                    LastChangedByUserId: evt.UserId ?? string.Empty);
            }

            lastTimestamp = evt.Timestamp;
        }

        // If truncated, include fields from current state that aren't in the blame map
        // with LastChangedAtSequence = -1 (changed before the analysis window)
        if (isTruncated && currentState is JsonObject finalState) {
            Dictionary<string, string> allFields = FlattenJson(finalState, prefix: string.Empty);
            foreach (KeyValuePair<string, string> field in allFields) {
                if (!blameMap.ContainsKey(field.Key)) {
                    blameMap[field.Key] = new FieldProvenance(
                        FieldPath: field.Key,
                        CurrentValue: field.Value,
                        PreviousValue: string.Empty,
                        LastChangedAtSequence: -1,
                        LastChangedAtTimestamp: DateTimeOffset.MinValue,
                        LastChangedByEventType: string.Empty,
                        LastChangedByCorrelationId: string.Empty,
                        LastChangedByUserId: string.Empty);
                }
            }
        }

        // Sort by FieldPath
        var fields = blameMap.Values
            .OrderBy(f => f.FieldPath, StringComparer.Ordinal)
            .ToList();

        // Field truncation: keep only the most recently changed fields
        bool isFieldsTruncated = fields.Count > maxFields;
        if (isFieldsTruncated) {
            fields = fields
                .OrderByDescending(f => f.LastChangedAtSequence)
                .Take(maxFields)
                .OrderBy(f => f.FieldPath, StringComparer.Ordinal)
                .ToList();
        }

        return new AggregateBlameView(
            tenantId, domain, aggregateId,
            AtSequence: atSequence,
            Timestamp: lastTimestamp,
            Fields: fields,
            IsTruncated: isTruncated,
            IsFieldsTruncated: isFieldsTruncated);
    }

    /// <summary>
    /// Counts how many watched fields differ between midpoint values and expected values
    /// using <see cref="JsonElement.DeepEquals"/> for semantic JSON comparison.
    /// </summary>
    private static int CountDivergentFields(
        Dictionary<string, JsonElement> midValues,
        Dictionary<string, JsonElement> expectedValues) {
        int count = 0;
        foreach (KeyValuePair<string, JsonElement> expected in expectedValues) {
            if (!midValues.TryGetValue(expected.Key, out JsonElement midValue)
                || !JsonElement.DeepEquals(midValue, expected.Value)) {
                count++;
            }
        }

        // Also count fields present in midValues but not in expectedValues
        foreach (string key in midValues.Keys) {
            if (!expectedValues.ContainsKey(key)) {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Deep-merges source into target (JSON object level).
    /// </summary>
    private static void DeepMerge(JsonObject target, JsonObject source) {
        foreach (KeyValuePair<string, JsonNode?> property in source) {
            if (property.Value is JsonObject sourceChild
                && target[property.Key] is JsonObject targetChild) {
                DeepMerge(targetChild, sourceChild);
            }
            else {
                target[property.Key] = property.Value?.DeepClone();
            }
        }
    }

    /// <summary>
    /// Extracts field values from a JSON state object for the specified field paths.
    /// Parses each leaf value as a <see cref="JsonElement"/> for semantic comparison.
    /// </summary>
    private static Dictionary<string, JsonElement> ExtractFieldValues(
        JsonObject state,
        IReadOnlyList<string> fieldPaths) {
        Dictionary<string, string> allLeafFields = FlattenJson(state, string.Empty);
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        foreach (string fieldPath in fieldPaths) {
            if (allLeafFields.TryGetValue(fieldPath, out string? jsonValue)) {
                try {
                    using var doc = JsonDocument.Parse(jsonValue);
                    result[fieldPath] = doc.RootElement.Clone();
                }
                catch (JsonException) {
                    // If the value cannot be parsed, treat as a raw string
                    using var fallbackDoc = JsonDocument.Parse($"\"{jsonValue}\"");
                    result[fieldPath] = fallbackDoc.RootElement.Clone();
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts sandbox event representations from domain result events.
    /// Falls back to the event's runtime type name when <see cref="ISerializedEventPayload"/> is not implemented.
    /// </summary>
    private static List<SandboxEvent> ExtractSandboxEvents(IReadOnlyList<IEventPayload> events) {
        List<SandboxEvent> result = new(events.Count);
        for (int i = 0; i < events.Count; i++) {
            IEventPayload evt = events[i];
            string eventTypeName;
            string eventPayloadJson;

            if (evt is ISerializedEventPayload serialized) {
                eventTypeName = serialized.EventTypeName;
                eventPayloadJson = Encoding.UTF8.GetString(serialized.PayloadBytes);
            }
            else {
                eventTypeName = evt.GetType().Name;
                eventPayloadJson = "{}";
            }

            result.Add(new SandboxEvent(
                Index: i,
                EventTypeName: eventTypeName,
                PayloadJson: eventPayloadJson,
                IsRejection: evt is IRejectionEvent));
        }

        return result;
    }

    /// <summary>
    /// Flattens a JSON object into leaf field paths and their values.
    /// </summary>
    private static Dictionary<string, string> FlattenJson(JsonObject obj, string prefix) {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, JsonNode?> prop in obj) {
            string fieldPath = string.IsNullOrEmpty(prefix) ? prop.Key : $"{prefix}.{prop.Key}";
            if (prop.Value is JsonObject childObj) {
                foreach (KeyValuePair<string, string> nested in FlattenJson(childObj, fieldPath)) {
                    result[nested.Key] = nested.Value;
                }
            }
            else {
                result[fieldPath] = prop.Value?.ToJsonString() ?? "null";
            }
        }

        return result;
    }

    /// <summary>
    /// Compares two JSON objects and returns changed fields with old/new values.
    /// </summary>
    private static Dictionary<string, (string NewValue, string OldValue)> JsonDiff(
        JsonObject? before,
        JsonObject? after,
        string prefix) {
        var changes = new Dictionary<string, (string NewValue, string OldValue)>(StringComparer.Ordinal);

        if (after is null) {
            return changes;
        }

        foreach (KeyValuePair<string, JsonNode?> prop in after) {
            string fieldPath = string.IsNullOrEmpty(prefix) ? prop.Key : $"{prefix}.{prop.Key}";
            JsonNode? beforeValue = before?[prop.Key];
            JsonNode? afterValue = prop.Value;

            if (afterValue is JsonObject afterObj) {
                // Recurse into nested objects
                Dictionary<string, (string NewValue, string OldValue)> nested = JsonDiff(
                    beforeValue as JsonObject,
                    afterObj,
                    fieldPath);
                foreach (KeyValuePair<string, (string NewValue, string OldValue)> n in nested) {
                    changes[n.Key] = n.Value;
                }
            }
            else {
                string afterStr = afterValue?.ToJsonString() ?? "null";
                string beforeStr = beforeValue?.ToJsonString() ?? "null";

                if (!string.Equals(afterStr, beforeStr, StringComparison.Ordinal)) {
                    changes[fieldPath] = (afterStr, beforeStr == "null" ? string.Empty : beforeStr);
                }
            }
        }

        // Check for fields removed in 'after' (present in before, missing in after)
        if (before is not null) {
            foreach (KeyValuePair<string, JsonNode?> prop in before) {
                string fieldPath = string.IsNullOrEmpty(prefix) ? prop.Key : $"{prefix}.{prop.Key}";
                if (!after.ContainsKey(prop.Key) && prop.Value is not null) {
                    changes[fieldPath] = ("null", prop.Value.ToJsonString());
                }
            }
        }

        return changes;
    }

    /// <summary>
    /// Reconstructs aggregate state by replaying events up to the specified sequence number.
    /// Uses the same JSON merge strategy as the blame algorithm.
    /// </summary>
    private static JsonObject ReconstructState(ServerEventEnvelope[] allEvents, long upToSequence) {
        var state = new JsonObject();
        foreach (ServerEventEnvelope evt in allEvents) {
            if (evt.SequenceNumber > upToSequence) {
                break;
            }

            try {
                var eventPayload = JsonNode.Parse(evt.Payload);
                if (eventPayload is JsonObject payloadObj) {
                    DeepMerge(state, payloadObj);
                }
            }
            catch (JsonException) {
                // Event payload is not valid JSON — skip
                continue;
            }
        }

        return state;
    }
}
