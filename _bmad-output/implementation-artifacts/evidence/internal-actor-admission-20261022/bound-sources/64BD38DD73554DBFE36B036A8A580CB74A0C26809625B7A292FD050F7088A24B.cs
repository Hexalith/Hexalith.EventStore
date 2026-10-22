using Hexalith.EventStore.Client.Processes;

namespace Hexalith.EventStore.Client.Reminders;

/// <summary>Optional read-only keyset discovery of committed reminder targets before their first convergence.</summary>
/// <remarks>Each source tuple denotes a target. Reuse authoritative retained membership; never infer targets from a configured tenant list.</remarks>
public interface IReminderTargetCatalog
{
    /// <summary>Returns canonical target tuples strictly after the requested key with a bounded page size.</summary>
    Task<ProcessSourcePage> ListAsync(string? afterKey, int limit, CancellationToken cancellationToken = default);
}
