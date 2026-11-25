using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The committed execution progress of an admitted task.</summary>
/// <param name="Receipts">The immutable owner receipt identifiers in checkpoint order, starting with step one.</param>
/// <param name="EffectPending">Whether the next step may have reached its owner.</param>
/// <param name="NeedsAttention">Whether automatic execution is fenced pending reconciliation.</param>
public sealed record DurableTaskProgress(ImmutableArray<string> Receipts, bool EffectPending, bool NeedsAttention)
{
    /// <summary>The number of steps with committed receipts.</summary>
    [JsonIgnore]
    public int Checkpoint => Receipts.Length;
}
