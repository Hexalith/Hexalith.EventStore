using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

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
    IAggregateStateReconstructor aggregateStateReconstructor,
    ILogger<AdminStreamQueryController> logger,
    IEventPayloadProtectionService? payloadProtectionService = null) : ControllerBase {
    private const int _defaultMaxBisectFields = 1_000;
    private const int _defaultMaxBisectSteps = 30;
    private const int _defaultMaxEvents = 10_000;
    private const int _defaultMaxFields = 5_000;
    private const int _maxDirectBisectFields = _defaultMaxBisectFields;
    private const int _maxDirectBisectSteps = _defaultMaxBisectSteps;
    private const int _maxDirectBlameEvents = _defaultMaxEvents;
    private const int _maxDirectBlameFields = _defaultMaxFields;
    private const int _maxDirectTimelineCount = 1_000;

    private const string _replayProblemTypePrefix = "urn:hexalith:eventstore:replay:";
    private readonly IEventPayloadProtectionService _payloadProtectionService = payloadProtectionService ?? new NoOpEventPayloadProtectionService();

    private static readonly JsonDocumentOptions _payloadParseOptions = new() { MaxDepth = 64 };

    /// <summary>
    /// Returns the reconstructed aggregate state at the requested sequence position. Replay
    /// is performed by the owning domain service via the canonical <c>POST /replay-state</c>
    /// path; this controller never reconstructs state from raw payloads.
    /// </summary>
    [HttpGet("{tenantId}/{domain}/{aggregateId}/state")]
    [ProducesResponseType(typeof(AggregateStateSnapshot), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetAggregateStateAsync(
        string tenantId,
        string domain,
        string aggregateId,
        [FromQuery] long at,
        CancellationToken ct = default) {
        if (at < 0) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Parameter 'at' must be >= 0.");
        }

        ct.ThrowIfCancellationRequested();

        try {
            if (at == 0) {
                // Cheap baseline: clients use at=0 to obtain the empty pre-genesis state
                // for diffing without paying for an actor round-trip or replay invocation.
                // Existence is verified for at>0; this short-circuit is intentional.
                return Ok(new AggregateStateSnapshot(
                    TenantId: tenantId,
                    Domain: domain,
                    AggregateId: aggregateId,
                    SequenceNumber: 0,
                    Timestamp: DateTimeOffset.UnixEpoch,
                    StateJson: "{}"));
            }

            var identity = new AggregateIdentity(tenantId, domain, aggregateId);
            IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId), "AggregateActor");

            ServerEventEnvelope[] allEvents = await actor.GetEventsAsync(0).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            if (allEvents.Length == 0) {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: "Stream not found or has no events.");
            }

            long maxSequence = allEvents[^1].SequenceNumber;
            if (at > maxSequence) {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Sequence {at} exceeds latest event sequence {maxSequence}.");
            }

            ServerEventEnvelope? eventAt = allEvents
                .Where(e => e.SequenceNumber <= at)
                .OrderByDescending(e => e.SequenceNumber)
                .FirstOrDefault();
            if (eventAt is null) {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: "No events found at or before the specified sequence.");
            }

            AggregateReconstructionResult replay = await aggregateStateReconstructor
                .ReconstructAsync(identity, eventAt.AggregateType, allEvents, at, includeTimeline: false, requestId: null, ct)
                .ConfigureAwait(false);
            IActionResult? failure = MapReplayFailureToProblem(replay);
            if (failure is not null) {
                return failure;
            }

            if (string.IsNullOrWhiteSpace(replay.StateJson)) {
                return MalformedReplayResultProblem(replay, $"Replay result is missing valid state JSON for sequence {at}.", at);
            }

            return Ok(new AggregateStateSnapshot(
                TenantId: tenantId,
                Domain: domain,
                AggregateId: aggregateId,
                SequenceNumber: at,
                Timestamp: eventAt.Timestamp,
                StateJson: replay.StateJson));
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (ArgumentException ex) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: ex.Message);
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to reconstruct aggregate state for {TenantId}/{Domain}/{AggregateId} at {At}.",
                tenantId, domain, aggregateId, at);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "Failed to reconstruct aggregate state.");
        }
    }

    /// <summary>
    /// Returns the diff of aggregate state between two sequence positions. Reuses the same
    /// state replay and <see cref="JsonDiff"/> helper as <c>/step</c> and <c>/bisect</c>.
    /// </summary>
    [HttpGet("{tenantId}/{domain}/{aggregateId}/diff")]
    [ProducesResponseType(typeof(AggregateStateDiff), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DiffAggregateStateAsync(
        string tenantId,
        string domain,
        string aggregateId,
        [FromQuery] long from,
        [FromQuery] long to,
        CancellationToken ct = default) {
        if (from < 0) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Parameter 'from' must be >= 0.");
        }

        if (to <= from) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Parameter 'to' must be greater than 'from'.");
        }

        ct.ThrowIfCancellationRequested();

        try {
            var identity = new AggregateIdentity(tenantId, domain, aggregateId);
            IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId), "AggregateActor");

            ServerEventEnvelope[] allEvents = await actor.GetEventsAsync(0).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            if (allEvents.Length == 0) {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: "Stream not found or has no events.");
            }

            long maxSequence = allEvents[^1].SequenceNumber;
            if (to > maxSequence) {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Sequence 'to'={to} exceeds latest event sequence {maxSequence}.");
            }

            // Single replay with timeline=true gives us state at every sequence in one
            // round trip, including state at `from` (when from > 0) and state at `to`.
            var identityForReplay = new AggregateIdentity(tenantId, domain, aggregateId);
            AggregateReconstructionResult replay = await aggregateStateReconstructor
                .ReconstructAsync(
                    identityForReplay,
                    allEvents[^1].AggregateType,
                    allEvents,
                    to,
                    includeTimeline: true,
                    requestId: null,
                    ct)
                .ConfigureAwait(false);
            IActionResult? failure = MapReplayFailureToProblem(replay);
            if (failure is not null) {
                return failure;
            }

            JsonObject fromState;
            if (from == 0) {
                fromState = new JsonObject();
            }
            else if (!TryResolveTimelineState(replay.Timeline, from, out fromState)) {
                return MalformedReplayResultProblem(replay, $"Replay timeline is missing state for sequence {from}.", from);
            }

            JsonObject? parsedToState = ParseStateJson(replay.StateJson);
            if (parsedToState is null) {
                return MalformedReplayResultProblem(replay, $"Replay result is missing valid state JSON for sequence {to}.", to);
            }

            JsonObject toState = parsedToState;

            Dictionary<string, (string NewValue, string OldValue)> changes = JsonDiff(
                fromState, toState, prefix: string.Empty);

            IReadOnlyList<FieldChange> changedFields = [.. changes
                .Select(c => new FieldChange(c.Key, c.Value.OldValue, c.Value.NewValue))
                .OrderBy(c => c.FieldPath, StringComparer.Ordinal)];

            return Ok(new AggregateStateDiff(from, to, changedFields));
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (ArgumentException ex) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: ex.Message);
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to diff aggregate state for {TenantId}/{Domain}/{AggregateId} from {From} to {To}.",
                tenantId, domain, aggregateId, from, to);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "Failed to compute aggregate state diff.");
        }
    }

    /// <summary>
    /// Returns the truth-preserving causation chain rooted at the event at the requested
    /// sequence. Same-correlation events are not promoted to direct causation links unless
    /// proven through MessageId/CausationId linkage.
    /// </summary>
    [HttpGet("{tenantId}/{domain}/{aggregateId}/causation")]
    [ProducesResponseType(typeof(CausationChain), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TraceCausationChainAsync(
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

        ct.ThrowIfCancellationRequested();

        try {
            var identity = new AggregateIdentity(tenantId, domain, aggregateId);
            IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId), "AggregateActor");

            ServerEventEnvelope[] allEvents = await actor.GetEventsAsync(0).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            if (allEvents.Length == 0) {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: "Stream not found or has no events.");
            }

            ServerEventEnvelope? target = allEvents.FirstOrDefault(e => e.SequenceNumber == at);
            if (target is null) {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"No event at sequence {at}.");
            }

            // Truth-preserving chain: target event always present, plus only events that
            // are linked through MessageId/CausationId. Blank causation ids never fabricate
            // links and same-correlation grouping is intentionally excluded.
            var linked = new List<ServerEventEnvelope> { target };

            if (!string.IsNullOrWhiteSpace(target.CausationId)) {
                ServerEventEnvelope? upstream = allEvents
                    .Where(e => e.SequenceNumber != target.SequenceNumber
                        && string.Equals(e.MessageId, target.CausationId, StringComparison.Ordinal))
                    .OrderBy(e => e.SequenceNumber)
                    .FirstOrDefault();
                if (upstream is not null) {
                    linked.Add(upstream);
                }
            }

            foreach (ServerEventEnvelope downstream in allEvents) {
                if (downstream.SequenceNumber == target.SequenceNumber) {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(downstream.CausationId)) {
                    continue;
                }

                if (string.Equals(downstream.CausationId, target.MessageId, StringComparison.Ordinal)) {
                    linked.Add(downstream);
                }
            }

            IReadOnlyList<CausationEvent> chainEvents = [.. linked
                .DistinctBy(e => e.SequenceNumber)
                .OrderBy(e => e.SequenceNumber)
                .Select(e => new CausationEvent(
                    e.SequenceNumber,
                    string.IsNullOrWhiteSpace(e.EventTypeName) ? "unknown" : e.EventTypeName,
                    e.Timestamp))];

            // The event envelope does not store the originating command type. Use the target
            // event's metadata so the UI can show what event the chain starts from without
            // fabricating a command-side identifier we do not actually have. When the target
            // itself is missing identifiers (corrupt envelope), fall back to a deterministic
            // sequence-derived placeholder rather than throwing into the generic 500 path.
            string targetIdFallback = $"seq-{target.SequenceNumber}";
            string originatingCommandId = !string.IsNullOrWhiteSpace(target.CausationId)
                ? target.CausationId
                : !string.IsNullOrWhiteSpace(target.MessageId)
                    ? target.MessageId
                    : targetIdFallback;
            string originatingCommandType = !string.IsNullOrWhiteSpace(target.EventTypeName)
                ? target.EventTypeName
                : "unknown";
            string correlationId = !string.IsNullOrWhiteSpace(target.CorrelationId)
                ? target.CorrelationId
                : !string.IsNullOrWhiteSpace(target.MessageId)
                    ? target.MessageId
                    : targetIdFallback;

            CausationChain chain = new(
                OriginatingCommandType: originatingCommandType,
                OriginatingCommandId: originatingCommandId,
                CorrelationId: correlationId,
                UserId: string.IsNullOrWhiteSpace(target.UserId) ? null : target.UserId,
                Events: chainEvents,
                AffectedProjections: []);

            return Ok(chain);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (ArgumentException ex) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: ex.Message);
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to trace causation chain for {TenantId}/{Domain}/{AggregateId} at {At}.",
                tenantId, domain, aggregateId, at);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "Failed to trace causation chain.");
        }
    }

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

        if (maxSteps > _maxDirectBisectSteps) {
            return BadRequestWithReasonCode(
                $"Parameter 'maxSteps' exceeds the direct CommandApi maximum of {_maxDirectBisectSteps}.",
                "max_steps_above_limit");
        }

        if (maxFields > _maxDirectBisectFields) {
            return BadRequestWithReasonCode(
                $"Parameter 'maxFields' exceeds the direct CommandApi maximum of {_maxDirectBisectFields}.",
                "max_fields_above_limit");
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
                return BadRequestWithReasonCode(
                    $"Parameter 'bad' ({bad}) exceeds stream length ({actualMaxSequence}); choose a sequence within the stream before bisecting.",
                    "bad_above_stream");
            }

            // Single timeline replay covers every sequence the bisect/diff steps need.
            var bisectIdentity = new AggregateIdentity(tenantId, domain, aggregateId);
            AggregateReconstructionResult bisectReplay = await aggregateStateReconstructor
                .ReconstructAsync(bisectIdentity, allEvents[^1].AggregateType, allEvents, bad, includeTimeline: true, requestId: null, ct)
                .ConfigureAwait(false);
            IActionResult? bisectFailure = MapReplayFailureToProblem(bisectReplay);
            if (bisectFailure is not null) {
                return bisectFailure;
            }

            // Reconstruct state at the good sequence to establish expected field values
            JsonObject goodState;
            if (good == 0) {
                goodState = new JsonObject();
            }
            else if (!TryResolveTimelineState(bisectReplay.Timeline, good, out goodState)) {
                return MalformedReplayResultProblem(bisectReplay, $"Replay timeline is missing state for sequence {good}.", good);
            }

            Dictionary<string, string> allLeafFields = FlattenJson(goodState, string.Empty);

            // If good state is empty (e.g., good=0), use bad state's fields as the watch set
            // to avoid vacuous comparisons where all midpoints report "good"
            if (allLeafFields.Count == 0 && fieldPaths.Count == 0) {
                if (!TryResolveTimelineState(bisectReplay.Timeline, bad, out JsonObject badState)) {
                    return MalformedReplayResultProblem(bisectReplay, $"Replay timeline is missing state for sequence {bad}.", bad);
                }

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

                if (!TryResolveTimelineState(bisectReplay.Timeline, mid, out JsonObject midState)) {
                    return MalformedReplayResultProblem(bisectReplay, $"Replay timeline is missing state for sequence {mid}.", mid);
                }

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
                JsonObject stateBeforeDivergent;
                if (badSeq <= 1) {
                    stateBeforeDivergent = new JsonObject();
                }
                else if (!TryResolveTimelineState(bisectReplay.Timeline, badSeq - 1, out stateBeforeDivergent)) {
                    return MalformedReplayResultProblem(bisectReplay, $"Replay timeline is missing state for sequence {badSeq - 1}.", badSeq - 1);
                }

                if (!TryResolveTimelineState(bisectReplay.Timeline, badSeq, out JsonObject stateAtDivergent)) {
                    return MalformedReplayResultProblem(bisectReplay, $"Replay timeline is missing state for sequence {badSeq}.", badSeq);
                }

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
    /// Returns a paginated timeline of events for the specified aggregate stream.
    /// Used by the Admin UI Events page, StreamDetail page, MCP and CLI tools (FR69).
    /// </summary>
    [HttpGet("{tenantId}/{domain}/{aggregateId}/timeline")]
    [ProducesResponseType(typeof(PagedResult<TimelineEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetStreamTimelineAsync(
        string tenantId,
        string domain,
        string aggregateId,
        [FromQuery] long? from,
        [FromQuery] long? to,
        [FromQuery] int count = 100,
        CancellationToken ct = default) {
        if (from is < 0) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Parameter 'from' must be >= 0 when provided.");
        }

        if (to is < 1) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Parameter 'to' must be >= 1 when provided.");
        }

        if (from.HasValue && to.HasValue && to.Value < from.Value) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Parameter 'to' must be >= 'from'.");
        }

        if (count <= 0) {
            count = 100;
        }

        if (count > _maxDirectTimelineCount) {
            return BadRequestWithReasonCode(
                $"Parameter 'count' exceeds the direct CommandApi maximum of {_maxDirectTimelineCount}.",
                "count_above_limit");
        }

        try {
            var identity = new AggregateIdentity(tenantId, domain, aggregateId);
            IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId), "AggregateActor");

            // GetEventsAsync is exclusive on lower bound; subtract 1 to make AC inclusive.
            long fromSequence = from is > 0 ? from.Value - 1 : 0;
            ServerEventEnvelope[] allEvents = await actor.GetEventsAsync(fromSequence).ConfigureAwait(false);

            IEnumerable<ServerEventEnvelope> filtered = allEvents;
            if (to.HasValue) {
                filtered = filtered.Where(e => e.SequenceNumber <= to.Value);
            }

            List<ServerEventEnvelope> filteredEvents = [.. filtered.OrderBy(e => e.SequenceNumber)];
            List<TimelineEntry> entries = [.. filteredEvents
                .Take(count)
                .Select(e => new TimelineEntry(
                    e.SequenceNumber,
                    e.Timestamp,
                    TimelineEntryType.Event,
                    e.EventTypeName,
                    e.CorrelationId,
                    string.IsNullOrWhiteSpace(e.UserId) ? null : e.UserId))];

            // Cursor is exclusive: callers re-issue the request with `from = continuationToken` to fetch
            // the next page starting after the last entry of this page (since `from` is inclusive on the
            // controller and the cursor is the last entry's sequence + 1).
            string? continuationToken = filteredEvents.Count > entries.Count && entries.Count > 0
                ? (entries[^1].SequenceNumber + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null;

            return Ok(new PagedResult<TimelineEntry>(entries, filteredEvents.Count, continuationToken));
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to fetch stream timeline for {TenantId}/{Domain}/{AggregateId}.",
                tenantId, domain, aggregateId);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "Failed to fetch stream timeline.");
        }
    }

    /// <summary>
    /// Returns full detail for the event at the requested sequence number. Used by the Admin UI event detail
    /// panel, CLI stream commands, and MCP diagnostics. Protected payloads are returned only after
    /// successful unprotection; unreadable protected data returns a safe ProblemDetails response.
    /// </summary>
    [HttpGet("{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber:long}")]
    [ProducesResponseType(typeof(EventDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventDetailAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long sequenceNumber,
        CancellationToken ct = default) {
        if (sequenceNumber <= 0) {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Parameter 'sequenceNumber' must be >= 1.");
        }

        ct.ThrowIfCancellationRequested();

        try {
            var identity = new AggregateIdentity(tenantId, domain, aggregateId);
            IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId), "AggregateActor");

            // GetEventsAsync is exclusive on lower bound; subtract 1 to include the target sequence.
            ServerEventEnvelope[] events = await actor.GetEventsAsync(sequenceNumber - 1).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            ServerEventEnvelope? target = events.FirstOrDefault(e => e.SequenceNumber == sequenceNumber);
            if (target is null) {
                logger.LogInformation(
                    "Event detail not found for {TenantId}/{Domain}/{AggregateId} at sequence {SequenceNumber}.",
                    tenantId,
                    domain,
                    aggregateId,
                    sequenceNumber);
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: "Event not found.");
            }

            AdminReadabilityResult readable = await TryMakeAdminEventReadableAsync(identity, target, "admin-event-detail", ct)
                .ConfigureAwait(false);
            if (readable.UnreadableReason is not null) {
                return CreateUnreadableProtectedDataProblem(
                    readable.UnreadableReason.Value,
                    tenantId,
                    domain,
                    aggregateId,
                    sequenceNumber,
                    "admin-event-detail");
            }

            ServerEventEnvelope readableTarget = readable.Envelope!;
            string decoded = readableTarget.Payload is { Length: > 0 }
                ? Encoding.UTF8.GetString(readableTarget.Payload)
                : string.Empty;
            string payloadJson = string.IsNullOrWhiteSpace(decoded) ? "{}" : decoded;

            EventDetail detail = new(
                TenantId: tenantId,
                Domain: domain,
                AggregateId: aggregateId,
                SequenceNumber: readableTarget.SequenceNumber,
                EventTypeName: readableTarget.EventTypeName,
                Timestamp: readableTarget.Timestamp,
                CorrelationId: readableTarget.CorrelationId,
                CausationId: string.IsNullOrWhiteSpace(readableTarget.CausationId) ? null : readableTarget.CausationId,
                UserId: string.IsNullOrWhiteSpace(readableTarget.UserId) ? null : readableTarget.UserId,
                PayloadJson: payloadJson);

            return Ok(detail);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to fetch event detail for {TenantId}/{Domain}/{AggregateId} at sequence {SequenceNumber}.",
                tenantId, domain, aggregateId, sequenceNumber);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "Failed to fetch event detail.");
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
        CancellationToken ct = default) {
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

        if (maxEvents > _maxDirectBlameEvents) {
            return BadRequestWithReasonCode(
                $"Parameter 'maxEvents' exceeds the direct CommandApi maximum of {_maxDirectBlameEvents}.",
                "max_events_above_limit");
        }

        if (maxFields > _maxDirectBlameFields) {
            return BadRequestWithReasonCode(
                $"Parameter 'maxFields' exceeds the direct CommandApi maximum of {_maxDirectBlameFields}.",
                "max_fields_above_limit");
        }

        ct.ThrowIfCancellationRequested();

        try {
            var identity = new AggregateIdentity(tenantId, domain, aggregateId);
            IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId), "AggregateActor");

            ServerEventEnvelope[] allEvents = await actor.GetEventsAsync(0).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

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

            // Replay must always start from the beginning of the stream. The displayed
            // blame window may be truncated later, but the state snapshots remain absolute.
            ServerEventEnvelope[] replayEvents = allEvents
                .Where(e => e.SequenceNumber <= atSequence)
                .OrderBy(e => e.SequenceNumber)
                .ToArray();

            if (replayEvents.Length == 0) {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: "No events found at or before the specified sequence.");
            }

            ServerEventEnvelope[] eventsInRange = replayEvents;

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

            // Single timeline replay drives blame state-after-each-event via the canonical Apply path.
            string blameAggregateType = replayEvents[0].AggregateType;
            AggregateReconstructionResult blameReplay = await aggregateStateReconstructor
                .ReconstructAsync(identity, blameAggregateType, replayEvents, atSequence, includeTimeline: true, requestId: null, ct)
                .ConfigureAwait(false);
            IActionResult? blameFailure = MapReplayFailureToProblem(blameReplay);
            if (blameFailure is not null) {
                return blameFailure;
            }

            JsonObject blameBaseline;
            if (startSequence <= 1) {
                blameBaseline = new JsonObject();
            }
            else if (!TryResolveTimelineState(blameReplay.Timeline, startSequence - 1, out blameBaseline)) {
                return MalformedReplayResultProblem(blameReplay, $"Replay timeline is missing state for sequence {startSequence - 1}.", startSequence - 1);
            }

            foreach (ServerEventEnvelope evt in eventsInRange) {
                if (!TryResolveTimelineState(blameReplay.Timeline, evt.SequenceNumber, out _)) {
                    return MalformedReplayResultProblem(blameReplay, $"Replay timeline is missing state for sequence {evt.SequenceNumber}.", evt.SequenceNumber);
                }
            }

            AggregateBlameView result = ComputeBlame(
                tenantId, domain, aggregateId,
                eventsInRange, atSequence, isTruncated, maxFields, blameReplay.Timeline, blameBaseline);

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

            // Replay through the canonical Apply path with timeline=true so we can pluck
            // state at N (current) and state at N-1 (previous) without any payload deep-merge.
            var stepIdentity = new AggregateIdentity(tenantId, domain, aggregateId);
            AggregateReconstructionResult stepReplay = await aggregateStateReconstructor
                .ReconstructAsync(stepIdentity, targetEvent.AggregateType, allEvents, at, includeTimeline: true, requestId: null, ct)
                .ConfigureAwait(false);
            IActionResult? stepFailure = MapReplayFailureToProblem(stepReplay);
            if (stepFailure is not null) {
                return stepFailure;
            }

            JsonObject? parsedCurrentState = ParseStateJson(stepReplay.StateJson);
            if (parsedCurrentState is null) {
                return MalformedReplayResultProblem(stepReplay, $"Replay result is missing valid state JSON for sequence {at}.", at);
            }

            JsonObject stateAtCurrent = parsedCurrentState;
            JsonObject stateAtPrev;
            if (at <= 1) {
                stateAtPrev = new JsonObject();
            }
            else if (!TryResolveTimelineState(stepReplay.Timeline, at - 1, out stateAtPrev)) {
                return MalformedReplayResultProblem(stepReplay, $"Replay timeline is missing state for sequence {at - 1}.", at - 1);
            }

            string stateJson = stateAtCurrent.ToJsonString();

            // Compute field changes between previous state and current state
            Dictionary<string, (string NewValue, string OldValue)> changes = JsonDiff(stateAtPrev, stateAtCurrent, string.Empty);
            var fieldChanges = changes
                .Where(c => IsValidFieldPath(c.Key))
                .Select(c => new FieldChange(c.Key, c.Value.OldValue, c.Value.NewValue))
                .OrderBy(fc => fc.FieldPath, StringComparer.Ordinal)
                .ToList();

            AdminReadabilityResult readable = await TryMakeAdminEventReadableAsync(identity, targetEvent, "admin-event-step", ct)
                .ConfigureAwait(false);
            if (readable.UnreadableReason is not null) {
                return CreateUnreadableProtectedDataProblem(
                    readable.UnreadableReason.Value,
                    tenantId,
                    domain,
                    aggregateId,
                    at,
                    "admin-event-step");
            }

            ServerEventEnvelope readableTargetEvent = readable.Envelope!;

            // Get event payload as JSON string
            string eventPayloadJson;
            try {
                eventPayloadJson = System.Text.Encoding.UTF8.GetString(readableTargetEvent.Payload);
            }
            catch {
                eventPayloadJson = "{}";
            }

            EventStepFrame frame = new(
                TenantId: tenantId,
                Domain: domain,
                AggregateId: aggregateId,
                SequenceNumber: at,
                EventTypeName: readableTargetEvent.EventTypeName ?? string.Empty,
                Timestamp: readableTargetEvent.Timestamp,
                CorrelationId: readableTargetEvent.CorrelationId ?? string.Empty,
                CausationId: readableTargetEvent.CausationId ?? string.Empty,
                UserId: readableTargetEvent.UserId ?? string.Empty,
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
            // Step 2: Reconstruct state via the canonical Apply path
            ServerEventEnvelope[] allEvents;
            JsonObject inputState;
            long atSequence;
            string sandboxAggregateType = string.Empty;
            var sandboxIdentity = new AggregateIdentity(tenantId, domain, aggregateId);

            if (request.AtSequence == 0) {
                // Empty initial state — no stream lookup needed
                allEvents = [];
                inputState = [];
                atSequence = 0;
            }
            else {
                IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                    new ActorId(sandboxIdentity.ActorId), "AggregateActor");

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

                sandboxAggregateType = allEvents[^1].AggregateType;
                AggregateReconstructionResult sandboxInputReplay = await aggregateStateReconstructor
                    .ReconstructAsync(sandboxIdentity, sandboxAggregateType, allEvents, atSequence, includeTimeline: false, requestId: null, ct)
                    .ConfigureAwait(false);
                IActionResult? sandboxFailure = MapReplayFailureToProblem(sandboxInputReplay);
                if (sandboxFailure is not null) {
                    return sandboxFailure;
                }

                JsonObject? parsedInputState = ParseStateJson(sandboxInputReplay.StateJson);
                if (parsedInputState is null) {
                    return MalformedReplayResultProblem(sandboxInputReplay, $"Replay result is missing valid state JSON for sequence {atSequence}.", atSequence);
                }

                inputState = parsedInputState;
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
                logger.LogWarning(ex, "Domain service invocation failed during sandbox command for {TenantId}/{Domain}/{AggregateId}.",
                    tenantId, domain, aggregateId);
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
                    ErrorMessage: BuildSandboxInvocationErrorMessage(request.CommandType),
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

                // Apply produced events through the canonical Apply path. We synthesize
                // ServerEventEnvelopes for the produced events using sequence numbers immediately
                // after atSequence and append them to the input event list for replay.
                ServerEventEnvelope[] synthesized = SynthesizeSandboxEnvelopes(
                    producedEvents,
                    tenantId,
                    domain,
                    aggregateId,
                    sandboxAggregateType,
                    atSequence,
                    commandEnvelope.CorrelationId,
                    commandEnvelope.MessageId);
                ServerEventEnvelope[] sandboxBaseEvents = [.. allEvents.Where(e => e.SequenceNumber <= atSequence)];
                ServerEventEnvelope[] combined = new ServerEventEnvelope[sandboxBaseEvents.Length + synthesized.Length];
                Array.Copy(sandboxBaseEvents, combined, sandboxBaseEvents.Length);
                Array.Copy(synthesized, 0, combined, sandboxBaseEvents.Length, synthesized.Length);

                long sandboxTarget = atSequence + producedEvents.Count;
                AggregateReconstructionResult sandboxResultingReplay = await aggregateStateReconstructor
                    .ReconstructAsync(sandboxIdentity, sandboxAggregateType, combined, sandboxTarget, includeTimeline: false, requestId: null, ct)
                    .ConfigureAwait(false);
                IActionResult? sandboxResultingFailure = MapReplayFailureToProblem(sandboxResultingReplay);
                if (sandboxResultingFailure is not null) {
                    return sandboxResultingFailure;
                }

                JsonObject? parsedResultingState = ParseStateJson(sandboxResultingReplay.StateJson);
                if (parsedResultingState is null) {
                    return MalformedReplayResultProblem(sandboxResultingReplay, $"Replay result is missing valid state JSON for sequence {sandboxTarget}.", sandboxTarget);
                }

                JsonObject resultingState = parsedResultingState;
                resultingStateJson = resultingState.ToJsonString();

                // Compute state diff
                Dictionary<string, (string NewValue, string OldValue)> changes = JsonDiff(inputState, resultingState, string.Empty);
                stateChanges = changes
                    .Where(c => IsValidFieldPath(c.Key))
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
        int maxFields,
        IReadOnlyList<AggregateReconstructionTimelineEntry>? timeline,
        JsonObject initialState) {
        // blame_map: fieldPath -> FieldProvenance
        var blameMap = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal);
        JsonObject previousState = initialState;
        JsonObject currentState = initialState;
        DateTimeOffset lastTimestamp = DateTimeOffset.MinValue;

        // The timeline carries one entry per applied event in stream sequence order. Pair it
        // with the source envelope by SequenceNumber so blame metadata (correlation, user) stays
        // aligned with the Apply-driven state snapshot.
        Dictionary<long, AggregateReconstructionTimelineEntry> timelineBySequence = timeline is null
            ? new Dictionary<long, AggregateReconstructionTimelineEntry>()
            : timeline.ToDictionary(t => t.SequenceNumber);

        foreach (ServerEventEnvelope evt in events) {
            if (!timelineBySequence.TryGetValue(evt.SequenceNumber, out AggregateReconstructionTimelineEntry? entry)) {
                // Domain replay skipped this event (e.g., partial replay before the at sequence).
                // Without an authoritative state-after-event snapshot we cannot blame fields to it.
                continue;
            }

            previousState = currentState;
            currentState = ParseStateJson(entry.StateJson)!;

            // Diff previous and current state to find changed fields
            Dictionary<string, (string NewValue, string OldValue)> changes = JsonDiff(
                previousState,
                currentState,
                prefix: string.Empty);

            foreach (KeyValuePair<string, (string NewValue, string OldValue)> change in changes) {
                if (!IsValidFieldPath(change.Key)) {
                    continue;
                }

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

    private ObjectResult BadRequestWithReasonCode(string detail, string reasonCode) {
        ObjectResult problem = (ObjectResult)Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            detail: detail);
        if (problem.Value is ProblemDetails pd) {
            pd.Extensions["reasonCode"] = reasonCode;
        }

        return problem;
    }

    private static bool IsValidFieldPath(string fieldPath)
        => !string.IsNullOrWhiteSpace(fieldPath)
            && !fieldPath.EndsWith(".", StringComparison.Ordinal)
            && !fieldPath.Contains("..", StringComparison.Ordinal);

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
    /// Maps a domain replay result to an RFC 7807 ProblemDetails when the status is not
    /// <see cref="AggregateReconstructionStatus.Succeeded"/>. Returns null on success so
    /// the caller continues to its happy-path return. The HTTP status, type URI, and
    /// extension fields follow the Failure and HTTP Semantics Matrix in
    /// admin-ui-aggregate-state-replay-correctness.
    /// </summary>
    private IActionResult? MapReplayFailureToProblem(AggregateReconstructionResult replay) {
        if (replay.Status == AggregateReconstructionStatus.Succeeded) {
            return null;
        }

        (int statusCode, string typeSlug, string title) = replay.ErrorCategory switch {
            AggregateReconstructionErrorCategory.UnknownAggregateType => (StatusCodes.Status404NotFound, "unknown-aggregate-type", "Unknown aggregate type"),
            AggregateReconstructionErrorCategory.UnknownEventType => (StatusCodes.Status422UnprocessableEntity, "unknown-event-type", "Unknown event type"),
            AggregateReconstructionErrorCategory.DeserializationFailed => (StatusCodes.Status422UnprocessableEntity, "deserialization-failed", "Replay deserialization failed"),
            AggregateReconstructionErrorCategory.ApplyHandlerMissing => (StatusCodes.Status422UnprocessableEntity, "apply-handler-missing", "Apply handler missing"),
            AggregateReconstructionErrorCategory.ApplyFailed => (StatusCodes.Status409Conflict, "apply-failed", "Replay partially applied"),
            AggregateReconstructionErrorCategory.UnsupportedVersion => (StatusCodes.Status422UnprocessableEntity, "unsupported-version", "Unsupported event version"),
            _ => (StatusCodes.Status500InternalServerError, "unexpected", "Unexpected replay failure"),
        };

        string safeMessage = SafeReplayProblemMessage(replay, title);
        ObjectResult problem = (ObjectResult)Problem(
            statusCode: statusCode,
            title: title,
            detail: safeMessage,
            type: _replayProblemTypePrefix + typeSlug);
        if (problem.Value is ProblemDetails pd) {
            pd.Extensions["status"] = replay.Status.ToString();
            pd.Extensions["failedSequenceNumber"] = replay.FailedSequenceNumber;
            pd.Extensions["failedEventType"] = replay.FailedEventType ?? string.Empty;
            pd.Extensions["errorCategory"] = replay.ErrorCategory.ToString();
            pd.Extensions["message"] = safeMessage;
            pd.Extensions["lastAppliedSequenceNumber"] = replay.LastAppliedSequenceNumber;
        }

        logger.LogWarning(
            "Aggregate replay returned non-success: Status={Status}, Category={Category}, FailedSeq={FailedSequence}, FailedType={FailedEventType}, LastApplied={LastApplied}",
            replay.Status,
            replay.ErrorCategory,
            replay.FailedSequenceNumber,
            replay.FailedEventType,
            replay.LastAppliedSequenceNumber);
        return problem;
    }

    private IActionResult MalformedReplayResultProblem(AggregateReconstructionResult replay, string message, long sequence)
        => MapReplayFailureToProblem(AggregateReconstructionResult.Failed(
            AggregateReconstructionErrorCategory.Unexpected,
            message,
            failedSequenceNumber: sequence,
            failedEventType: string.Empty,
            lastAppliedSequenceNumber: replay.LastAppliedSequenceNumber))!;

    private async Task<AdminReadabilityResult> TryMakeAdminEventReadableAsync(
        AggregateIdentity identity,
        ServerEventEnvelope envelope,
        string stage,
        CancellationToken cancellationToken) {
        EventStorePayloadProtectionMetadata storedMetadata = EventStorePayloadProtectionMetadataCarrier
            .Read(envelope.Extensions);

        if (storedMetadata.State == PayloadProtectionState.ProviderOpaque) {
            return AdminReadabilityResult.Unreadable(
                UnreadableProtectedDataReasonMapper.FromProviderOpaqueMetadata(storedMetadata));
        }

        if (storedMetadata.State == PayloadProtectionState.Unprotected) {
            return AdminReadabilityResult.Readable(envelope);
        }

        PayloadUnprotectionOutcome outcome;
        try {
            outcome = await _payloadProtectionService
                .TryUnprotectEventPayloadAsync(
                    identity,
                    envelope.EventTypeName,
                    envelope.Payload,
                    envelope.SerializationFormat,
                    storedMetadata,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch {
            logger.LogWarning(
                "Protected event unreadable during admin inspection: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, SequenceNumber={SequenceNumber}, ReasonCode={ReasonCode}, Stage={Stage}",
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                envelope.SequenceNumber,
                UnreadableProtectedDataReasonCodes.ProviderUnavailable,
                stage);
            return AdminReadabilityResult.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable);
        }

        if (outcome.IsUnreadable) {
            return AdminReadabilityResult.Unreadable(outcome.UnreadableReason!.Value);
        }

        return AdminReadabilityResult.Readable(envelope with {
            Payload = outcome.PayloadBytes!,
            SerializationFormat = outcome.SerializationFormat!,
            Extensions = EventStorePayloadProtectionMetadataCarrier.Write(envelope.Extensions, outcome.Metadata),
        });
    }

    private ObjectResult CreateUnreadableProtectedDataProblem(
        UnreadableProtectedDataReason reason,
        string tenantId,
        string domain,
        string aggregateId,
        long sequenceNumber,
        string stage) {
        int statusCode = UnreadableProtectedDataProblem.GetStatusCode(reason);
        string reasonCode = UnreadableProtectedDataReasonCodes.From(reason);
        ObjectResult result = Problem(
            statusCode: statusCode,
            type: UnreadableProtectedDataProblem.TypeUri,
            title: UnreadableProtectedDataProblem.DefaultTitle,
            detail: UnreadableProtectedDataProblem.GetSafeOperatorGuidance(reason));

        if (result.Value is ProblemDetails problem) {
            problem.Extensions["reasonCode"] = reasonCode;
            problem.Extensions[UnreadableProtectedDataProblem.ExtensionReasonCategory] = reason.ToString();
            problem.Extensions[UnreadableProtectedDataProblem.ExtensionStage] = stage;
            problem.Extensions[UnreadableProtectedDataProblem.ExtensionSequenceNumber] = sequenceNumber;
            problem.Extensions[GatewayProblemDetailsExtensions.TenantId] = tenantId;
            problem.Extensions[UnreadableProtectedDataProblem.ExtensionDomain] = domain;
            problem.Extensions[UnreadableProtectedDataProblem.ExtensionAggregateId] = aggregateId;
            problem.Extensions[UnreadableProtectedDataProblem.ExtensionRetryable] = UnreadableProtectedDataReasonCodes.IsRetryable(reason);
            problem.Extensions[UnreadableProtectedDataProblem.ExtensionPermanent] = UnreadableProtectedDataReasonCodes.IsPermanent(reason);
        }

        return result;
    }

    private sealed record AdminReadabilityResult(
        ServerEventEnvelope? Envelope,
        UnreadableProtectedDataReason? UnreadableReason) {
        public static AdminReadabilityResult Readable(ServerEventEnvelope envelope)
            => new(envelope, null);

        public static AdminReadabilityResult Unreadable(UnreadableProtectedDataReason reason)
            => new(null, reason);
    }

    private static string SafeReplayProblemMessage(AggregateReconstructionResult replay, string title) {
        string sequenceSuffix = replay.FailedSequenceNumber.HasValue
            ? $" at sequence {replay.FailedSequenceNumber.Value}"
            : string.Empty;
        return $"{title}{sequenceSuffix}.";
    }

    private static string BuildSandboxInvocationErrorMessage(string commandType) {
        const string baseMessage = "Domain service invocation failed. Verify the command type is registered for this domain and that the payload matches the command schema.";
        return commandType.Contains(".Events.", StringComparison.OrdinalIgnoreCase)
            ? baseMessage + " The supplied type looks like an event type; Sandbox expects a command type."
            : baseMessage;
    }

    /// <summary>
    /// Parses a JSON state document into a <see cref="JsonObject"/>. Returns null when the
    /// payload is missing, empty, or not a JSON object (e.g., null literal or primitive).
    /// </summary>
    private static JsonObject? ParseStateJson(string? stateJson) {
        if (string.IsNullOrWhiteSpace(stateJson)) {
            return null;
        }

        try {
            return JsonNode.Parse(stateJson, documentOptions: _payloadParseOptions) as JsonObject;
        }
        catch (JsonException) {
            return null;
        }
    }

    private static bool TryResolveTimelineState(
        IReadOnlyList<AggregateReconstructionTimelineEntry>? timeline,
        long sequence,
        out JsonObject state) {
        state = new JsonObject();
        if (sequence <= 0) {
            return true;
        }

        if (timeline is null) {
            return false;
        }

        AggregateReconstructionTimelineEntry? entry = null;
        foreach (AggregateReconstructionTimelineEntry candidate in timeline) {
            if (candidate.SequenceNumber == sequence) {
                entry = candidate;
                break;
            }
        }

        if (entry is null) {
            return false;
        }

        JsonObject? parsed = ParseStateJson(entry.StateJson);
        if (parsed is null) {
            return false;
        }

        state = parsed;
        return true;
    }

    /// <summary>
    /// Materializes synthetic <see cref="ServerEventEnvelope"/> instances for the events a
    /// sandbox command produced so they can be replayed through the canonical Apply path
    /// alongside the persisted stream.
    /// </summary>
    private static ServerEventEnvelope[] SynthesizeSandboxEnvelopes(
        IReadOnlyList<SandboxEvent> producedEvents,
        string tenantId,
        string domain,
        string aggregateId,
        string aggregateType,
        long startingSequence,
        string correlationId,
        string causationMessageId) {
        if (producedEvents.Count == 0) {
            return [];
        }

        ServerEventEnvelope[] result = new ServerEventEnvelope[producedEvents.Count];
        DateTimeOffset now = DateTimeOffset.UtcNow;
        long globalPosition = startingSequence;
        for (int i = 0; i < producedEvents.Count; i++) {
            SandboxEvent produced = producedEvents[i];
            byte[] payload = string.IsNullOrEmpty(produced.PayloadJson)
                ? Encoding.UTF8.GetBytes("{}")
                : Encoding.UTF8.GetBytes(produced.PayloadJson);
            long seq = startingSequence + i + 1;
            globalPosition = seq;
            result[i] = new ServerEventEnvelope(
                MessageId: $"sandbox-{seq}",
                AggregateId: aggregateId,
                AggregateType: aggregateType,
                TenantId: tenantId,
                Domain: domain,
                SequenceNumber: seq,
                GlobalPosition: globalPosition,
                Timestamp: now,
                CorrelationId: correlationId,
                CausationId: causationMessageId,
                UserId: string.Empty,
                DomainServiceVersion: "v1",
                EventTypeName: produced.EventTypeName,
                MetadataVersion: 1,
                SerializationFormat: "json",
                Payload: payload,
                Extensions: null);
        }

        return result;
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
            if (string.IsNullOrWhiteSpace(prop.Key)) {
                continue;
            }

            string fieldPath = string.IsNullOrEmpty(prefix) ? prop.Key : $"{prefix}.{prop.Key}";
            if (!IsValidFieldPath(fieldPath)) {
                continue;
            }

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
            if (string.IsNullOrWhiteSpace(prop.Key)) {
                continue;
            }

            string fieldPath = string.IsNullOrEmpty(prefix) ? prop.Key : $"{prefix}.{prop.Key}";
            if (!IsValidFieldPath(fieldPath)) {
                continue;
            }

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

        // Per the Decision Ledger in docs/operations/admin-debugging-json-large-stream-hardening.md,
        // omitted properties are a `preserved-limitation` — they are NOT synthesized as deletes.
        // DeepMerge never removes keys, so this branch is unreachable in normal reconstruction flow.
        return changes;
    }

}
