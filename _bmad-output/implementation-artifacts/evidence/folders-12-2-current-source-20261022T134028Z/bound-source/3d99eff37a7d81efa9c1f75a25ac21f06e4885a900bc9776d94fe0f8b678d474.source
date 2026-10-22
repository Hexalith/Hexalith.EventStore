using System.Reflection;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>
/// DW3 ATDD red-phase scaffolds for facade non-regression (AC #10). Public
/// response model shape and route names must remain compatible unless a
/// failing test proves the current shape is defective. These reflection-based
/// contract tests catch accidental property removal/renaming during DW3
/// implementation. They also pin the stable diagnostic vocabulary contract.
/// </summary>
public class Dw3FacadeCompatibilityAtddTests {
    /// <summary>
    /// Required public properties on each response model. If DW3 implementation
    /// drops or renames any of these, Admin UI / CLI / MCP contract breaks.
    /// </summary>
    private static readonly Dictionary<Type, string[]> _requiredProperties = new() {
        [typeof(AggregateBlameView)] = [
            "TenantId", "Domain", "AggregateId", "AtSequence", "Timestamp",
            "Fields", "IsTruncated", "IsFieldsTruncated",
        ],
        [typeof(BisectResult)] = [
            "TenantId", "Domain", "AggregateId", "GoodSequence", "DivergentSequence",
            "DivergentTimestamp", "DivergentEventType", "DivergentCorrelationId",
            "DivergentUserId", "DivergentFieldChanges", "WatchedFieldPaths",
            "Steps", "TotalSteps", "IsTruncated",
        ],
        [typeof(EventStepFrame)] = [
            "TenantId", "Domain", "AggregateId", "SequenceNumber", "EventTypeName",
            "Timestamp", "CorrelationId", "CausationId", "UserId",
            "EventPayloadJson", "StateJson", "FieldChanges", "TotalEvents",
            "HasPrevious", "HasNext",
        ],
        [typeof(SandboxResult)] = [
            "TenantId", "Domain", "AggregateId", "AtSequence", "CommandType",
            "Outcome", "ProducedEvents", "ResultingStateJson", "StateChanges",
            "ErrorMessage", "ExecutionTimeMs",
        ],
        [typeof(CorrelationTraceMap)] = [
            "CorrelationId", "TenantId", "Domain", "AggregateId", "CommandType",
            "CommandStatus", "UserId", "CommandReceivedAt", "CommandCompletedAt",
            "DurationMs", "ProducedEvents", "AffectedProjections",
            "RejectionEventType", "ErrorMessage", "ExternalTraceUrl",
            "TotalStreamEvents", "ScanCapped", "ScanCapMessage",
        ],
        [typeof(FieldChange)] = [
            "FieldPath", "OldValue", "NewValue",
        ],
        [typeof(TimelineEntry)] = [
            "SequenceNumber", "Timestamp", "EntryType", "TypeName",
            "CorrelationId", "UserId",
        ],
    };

    [Fact]
    public void ResponseModels_PublicPropertiesUnchanged() {
        foreach (KeyValuePair<Type, string[]> kvp in _requiredProperties) {
            HashSet<string> actualProps = [.. kvp.Key
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)];

            foreach (string requiredProp in kvp.Value) {
                actualProps.ShouldContain(requiredProp,
                    $"DW3 AC#10: {kvp.Key.Name}.{requiredProp} must remain on the public response shape — "
                    + "Admin UI, CLI, and MCP rely on it.");
            }
        }
    }

    /// <summary>
    /// Pins both the public properties AND the runtime semantic of
    /// <c>PagedResult&lt;TimelineEntry&gt;.TotalCount</c>: it is the FULL filtered count
    /// available, NOT the returned-page size. This is required for AC #6 large-stream
    /// truncation visibility, and it is a documented contract change from the prior
    /// 'page size' semantic. The runtime-semantic check lives in
    /// <see cref="Dw3LargeStreamSurfaceAtddTests.Timeline_StreamLengthExceedsCount_ResponseExposesTruncationSignal"/>.
    /// </summary>
    [Fact]
    public void PagedResult_OfTimelineEntry_PublicPropertiesUnchanged() {
        Type pagedTimeline = typeof(PagedResult<TimelineEntry>);
        HashSet<string> actualProps = [.. pagedTimeline
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)];

        actualProps.ShouldContain("Items");
        actualProps.ShouldContain("TotalCount");
        actualProps.ShouldContain("ContinuationToken");
    }

    [Fact]
    public void ReasonCodeVocabulary_FollowsStableNamingContract() {
        // All bounded reason codes (direct-bound + trace-cap + JSON disposition labels)
        // must satisfy the regex contract pinned in Dw3TestUtilities.
        IEnumerable<string> allCodes =
        [
            .. Dw3TestUtilities.Dw3DirectBoundReasonCodes,
            Dw3TestUtilities.TraceScanCapReasonCode,
            .. Dw3TestUtilities.Dw3JsonBehaviorDispositions,
        ];

        foreach (string code in allCodes) {
            code.Length.ShouldBeLessThan(64,
                $"DW3 AC#10: code/disposition '{code}' must be < 64 chars.");
            // Allow lowercase letters, digits, underscores, and hyphens (dispositions
            // use hyphens — e.g. "preserved-limitation"). Reason codes use underscores.
            System.Text.RegularExpressions.Regex
                .IsMatch(code, "^[a-z][a-z0-9_-]*$")
                .ShouldBeTrue($"DW3 AC#10: '{code}' must match ^[a-z][a-z0-9_-]*$.");
        }
    }
}
