using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// Outcome of a single-stream consistency evaluation.
/// </summary>
/// <remarks>
/// DW12 design (decision record): consistency diagnostics depend on public EventStore
/// query contracts, not physical DAPR actor-state layout. Each outcome carries the
/// evaluated sequence range and checked event count so tests and operators can prove
/// the check executed, rather than inferring completeness from item counts.
/// </remarks>
/// <param name="Anomalies">Anomalies emitted for this stream.</param>
/// <param name="EvaluatedRange">The inclusive sequence range that was actually evaluated, when available.</param>
/// <param name="EvaluatedEventCount">The number of distinct event sequences observed inside the evaluated range.</param>
internal sealed record ConsistencyStreamCheckOutcome(
    IReadOnlyList<ConsistencyAnomaly> Anomalies,
    SequenceRange? EvaluatedRange,
    long EvaluatedEventCount);

/// <summary>
/// Inclusive sequence range evaluated during a continuity check.
/// </summary>
/// <param name="From">Inclusive lower bound (typically 1).</param>
/// <param name="To">Inclusive upper bound (typically <c>StreamSummary.LastEventSequence</c>).</param>
internal readonly record struct SequenceRange(long From, long To);
