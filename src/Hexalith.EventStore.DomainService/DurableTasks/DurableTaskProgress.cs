using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The committed execution progress of an admitted task.</summary>
/// <param name="Receipts">The immutable owner receipt identifiers in checkpoint order, starting with step one.</param>
/// <param name="EffectPending">Whether the next step may have reached its owner.</param>
/// <param name="Status">The committed lifecycle state.</param>
/// <param name="IrreversibleStep">The first irreversible step that may have reached its owner. Cancellation is refused once it is set.</param>
/// <param name="Attempts">The attempts at the pending step charged against the dependency failure budget. Each attempt is charged
/// when it starts. A committed receipt or a refusal resets the count, and an attempt the caller cancels is refunded. Zero when no
/// step is pending.</param>
public sealed record DurableTaskProgress(
    ImmutableArray<string> Receipts,
    bool EffectPending,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] DurableTaskStatus Status,
    int? IrreversibleStep = null,
    int Attempts = 0)
{
    /// <summary>The number of steps with committed receipts.</summary>
    [JsonIgnore]
    public int Checkpoint => Receipts.Length;

    /// <summary>Whether automatic execution is fenced pending reconciliation.</summary>
    [JsonIgnore]
    public bool NeedsAttention => Status == DurableTaskStatus.NeedsAttention;

    /// <summary>Whether the task has reached an immutable terminal state.</summary>
    [JsonIgnore]
    public bool IsTerminal => Status is DurableTaskStatus.Succeeded or DurableTaskStatus.Rejected
        or DurableTaskStatus.Failed or DurableTaskStatus.Cancelled;
}
