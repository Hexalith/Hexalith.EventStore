using System.Globalization;
using System.Text.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Replay;

namespace Hexalith.EventStore.Server.Tests.Projections;

internal static class ProjectionRebuildEquivalenceOracle {
    public static AggregateReconstructionResult Replay(
        string tenantId,
        string domain,
        string aggregateId,
        IReadOnlyList<ReplayEventEnvelope> events,
        long upToSequence) {
        var request = new AggregateReconstructionRequest(
            tenantId,
            domain,
            nameof(ProjectionRebuildAggregateState),
            aggregateId,
            upToSequence,
            events,
            IncludeTimeline: false,
            RequestId: null);
        return AggregateReplayer.Replay<ProjectionRebuildAggregateState>(request);
    }

    public static ProjectionRebuildEquivalenceSnapshot Build(
        string tenantId,
        string domain,
        string aggregateId,
        IReadOnlyList<ReplayEventEnvelope> events,
        long upToSequence,
        DateTimeOffset projectedAt) {
        AggregateReconstructionResult replay = Replay(
            tenantId,
            domain,
            aggregateId,
            events,
            upToSequence);
        if (replay.Status != AggregateReconstructionStatus.Succeeded
            || string.IsNullOrWhiteSpace(replay.StateJson)) {
            throw new InvalidOperationException(replay.Message ?? "Canonical aggregate replay failed.");
        }

        ProjectionRebuildAggregateState state = JsonSerializer.Deserialize<ProjectionRebuildAggregateState>(
            replay.StateJson,
            JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Canonical aggregate replay returned no state.");
        string projectionVersion = replay.LastAppliedSequenceNumber.ToString(CultureInfo.InvariantCulture);
        var detail = new AggregateReadModel(
            state.Id,
            state.Status,
            state.EventCount,
            projectedAt,
            projectionVersion);
        var index = new AggregateIndexReadModel(
            string.IsNullOrWhiteSpace(state.Id) ? [] : [state.Id],
            projectedAt,
            projectionVersion);
        return new ProjectionRebuildEquivalenceSnapshot(
            detail,
            index,
            projectionVersion,
            replay.LastAppliedSequenceNumber);
    }
}
