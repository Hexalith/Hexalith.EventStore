using System.Collections.Concurrent;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IProjectionSlotRegistry"/>. Populated at startup
/// (single-threaded DI configuration) and read concurrently at runtime.
/// </summary>
public sealed class ProjectionSlotRegistry : IProjectionSlotRegistry {
    private readonly ConcurrentDictionary<(string Projection, string Slot), ProjectionReadModelSlotKind> _slots = new();

    /// <inheritdoc/>
    public void Register(string projectionName, string slot, ProjectionReadModelSlotKind kind) {
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        ProjectionKeySegments.Validate(slot, nameof(slot));

        ProjectionReadModelSlotKind stored = _slots.GetOrAdd((projectionName, slot), kind);
        if (stored != kind) {
            throw new InvalidOperationException(
                $"Projection read-model slot '{slot}' for projection '{projectionName}' is already registered as "
                + $"'{stored}' and cannot be re-registered as '{kind}'.");
        }
    }

    /// <inheritdoc/>
    public bool TryGetKind(string projectionName, string slot, out ProjectionReadModelSlotKind kind) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);
        return _slots.TryGetValue((projectionName, slot), out kind);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAggregateOwnedSlots(string projectionName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);
        return [.. _slots
            .Where(entry => entry.Value == ProjectionReadModelSlotKind.AggregateOwned
                && string.Equals(entry.Key.Projection, projectionName, StringComparison.Ordinal))
            .Select(entry => entry.Key.Slot)
            .OrderBy(static slot => slot, StringComparer.Ordinal)];
    }
}
