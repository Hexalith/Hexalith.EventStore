using System.Text.Json;
using System.Text.Json.Nodes;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.CommandApi.Controllers;

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
    ILogger<AdminStreamQueryController> logger) : ControllerBase
{
    private const int DefaultMaxEvents = 10_000;
    private const int DefaultMaxFields = 5_000;

    /// <summary>
    /// Computes per-field blame (provenance) for an aggregate's state at a given sequence position.
    /// Uses an incremental O(N) algorithm: replay events, diff state after each apply, track blame.
    /// </summary>
    [HttpGet("{tenantId}/{domain}/{aggregateId}/blame")]
    [ProducesResponseType(typeof(AggregateBlameView), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAggregateBlame(
        string tenantId,
        string domain,
        string aggregateId,
        [FromQuery] long? at,
        [FromQuery] int maxEvents = DefaultMaxEvents,
        [FromQuery] int maxFields = DefaultMaxFields,
        CancellationToken ct = default)
    {
        if (at.HasValue && at.Value < 1)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Parameter 'at' must be >= 1 when provided.");
        }

        if (maxEvents <= 0)
        {
            maxEvents = DefaultMaxEvents;
        }

        if (maxFields <= 0)
        {
            maxFields = DefaultMaxFields;
        }

        try
        {
            var identity = new AggregateIdentity(tenantId, domain, aggregateId);
            IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId), "AggregateActor");

            ServerEventEnvelope[] allEvents = await actor.GetEventsAsync(0).ConfigureAwait(false);

            if (allEvents.Length == 0)
            {
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

            if (eventsInRange.Length == 0)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: "No events found at or before the specified sequence.");
            }

            // Truncation: if more events than maxEvents, start from a later position
            bool isTruncated = eventsInRange.Length > maxEvents;
            long startSequence = 1;
            if (isTruncated)
            {
                startSequence = atSequence - maxEvents + 1;
                if (startSequence < 1)
                {
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to compute blame for {TenantId}/{Domain}/{AggregateId} at {AtSequence}.",
                tenantId, domain, aggregateId, at);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "Failed to compute blame view.");
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
        int maxFields)
    {
        // blame_map: fieldPath -> FieldProvenance
        var blameMap = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal);
        JsonNode? previousState = JsonNode.Parse("{}");
        JsonNode? currentState = JsonNode.Parse("{}");
        DateTimeOffset lastTimestamp = DateTimeOffset.MinValue;

        foreach (ServerEventEnvelope evt in events)
        {
            previousState = currentState?.DeepClone();

            // Apply event payload to state using JSON merge
            try
            {
                JsonNode? eventPayload = JsonNode.Parse(evt.Payload);
                if (eventPayload is JsonObject payloadObj && currentState is JsonObject stateObj)
                {
                    DeepMerge(stateObj, payloadObj);
                }
            }
            catch (JsonException)
            {
                // Event payload is not valid JSON (e.g., binary format) — skip this event
                continue;
            }

            // Diff previous and current state to find changed fields
            Dictionary<string, (string NewValue, string OldValue)> changes = JsonDiff(
                previousState as JsonObject,
                currentState as JsonObject,
                prefix: string.Empty);

            foreach (KeyValuePair<string, (string NewValue, string OldValue)> change in changes)
            {
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
        if (isTruncated && currentState is JsonObject finalState)
        {
            Dictionary<string, string> allFields = FlattenJson(finalState, prefix: string.Empty);
            foreach (KeyValuePair<string, string> field in allFields)
            {
                if (!blameMap.ContainsKey(field.Key))
                {
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
        List<FieldProvenance> fields = blameMap.Values
            .OrderBy(f => f.FieldPath, StringComparer.Ordinal)
            .ToList();

        // Field truncation: keep only the most recently changed fields
        bool isFieldsTruncated = fields.Count > maxFields;
        if (isFieldsTruncated)
        {
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
    /// Deep-merges source into target (JSON object level).
    /// </summary>
    private static void DeepMerge(JsonObject target, JsonObject source)
    {
        foreach (KeyValuePair<string, JsonNode?> property in source)
        {
            if (property.Value is JsonObject sourceChild
                && target[property.Key] is JsonObject targetChild)
            {
                DeepMerge(targetChild, sourceChild);
            }
            else
            {
                target[property.Key] = property.Value?.DeepClone();
            }
        }
    }

    /// <summary>
    /// Compares two JSON objects and returns changed fields with old/new values.
    /// </summary>
    private static Dictionary<string, (string NewValue, string OldValue)> JsonDiff(
        JsonObject? before,
        JsonObject? after,
        string prefix)
    {
        var changes = new Dictionary<string, (string NewValue, string OldValue)>(StringComparer.Ordinal);

        if (after is null)
        {
            return changes;
        }

        foreach (KeyValuePair<string, JsonNode?> prop in after)
        {
            string fieldPath = string.IsNullOrEmpty(prefix) ? prop.Key : $"{prefix}.{prop.Key}";
            JsonNode? beforeValue = before?[prop.Key];
            JsonNode? afterValue = prop.Value;

            if (afterValue is JsonObject afterObj)
            {
                // Recurse into nested objects
                Dictionary<string, (string NewValue, string OldValue)> nested = JsonDiff(
                    beforeValue as JsonObject,
                    afterObj,
                    fieldPath);
                foreach (KeyValuePair<string, (string NewValue, string OldValue)> n in nested)
                {
                    changes[n.Key] = n.Value;
                }
            }
            else
            {
                string afterStr = afterValue?.ToJsonString() ?? "null";
                string beforeStr = beforeValue?.ToJsonString() ?? "null";

                if (!string.Equals(afterStr, beforeStr, StringComparison.Ordinal))
                {
                    changes[fieldPath] = (afterStr, beforeStr == "null" ? string.Empty : beforeStr);
                }
            }
        }

        // Check for fields removed in 'after' (present in before, missing in after)
        if (before is not null)
        {
            foreach (KeyValuePair<string, JsonNode?> prop in before)
            {
                string fieldPath = string.IsNullOrEmpty(prefix) ? prop.Key : $"{prefix}.{prop.Key}";
                if (!after.ContainsKey(prop.Key) && prop.Value is not null)
                {
                    changes[fieldPath] = ("null", prop.Value.ToJsonString());
                }
            }
        }

        return changes;
    }

    /// <summary>
    /// Flattens a JSON object into leaf field paths and their values.
    /// </summary>
    private static Dictionary<string, string> FlattenJson(JsonObject obj, string prefix)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, JsonNode?> prop in obj)
        {
            string fieldPath = string.IsNullOrEmpty(prefix) ? prop.Key : $"{prefix}.{prop.Key}";
            if (prop.Value is JsonObject childObj)
            {
                foreach (KeyValuePair<string, string> nested in FlattenJson(childObj, fieldPath))
                {
                    result[nested.Key] = nested.Value;
                }
            }
            else
            {
                result[fieldPath] = prop.Value?.ToJsonString() ?? "null";
            }
        }

        return result;
    }
}
