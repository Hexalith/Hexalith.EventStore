using System.Collections.Concurrent;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IProjectionSlotRegistry"/>. Populated at startup
/// (single-threaded DI configuration) and read concurrently at runtime.
/// </summary>
public sealed class ProjectionSlotRegistry : IProjectionSlotRegistry {
    private readonly ConcurrentDictionary<(string Projection, string Slot), ProjectionReadModelSlotDeclaration> _slots = new();
    private readonly ConcurrentDictionary<(string Domain, string Projection, string Slot), byte> _canonicalWriters = new();

    /// <inheritdoc/>
    public void Register(
        string projectionName,
        string slot,
        ProjectionReadModelSlotKind kind) {
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        ProjectionKeySegments.Validate(slot, nameof(slot));

        var declaration = new ProjectionReadModelSlotDeclaration(projectionName, slot, kind);
        ProjectionReadModelSlotDeclaration stored = _slots.GetOrAdd((projectionName, slot), declaration);
        if (stored.Kind != kind) {
            throw new InvalidOperationException(
                $"Projection read-model slot '{slot}' for projection '{projectionName}' is already registered as "
                + $"'{stored.Kind}' and cannot be re-registered as '{kind}'.");
        }
    }

    /// <inheritdoc/>
    public void RegisterCanonicalWriter(string domain, string projectionName, string slot) {
        ProjectionKeySegments.Validate(domain, nameof(domain));
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        ProjectionKeySegments.Validate(slot, nameof(slot));

        if (!_slots.TryGetValue((projectionName, slot), out ProjectionReadModelSlotDeclaration? declaration)
            || declaration.Kind != ProjectionReadModelSlotKind.AggregateOwned) {
            throw new InvalidOperationException(
                $"Canonical writer declaration for domain '{domain}', projection '{projectionName}', slot '{slot}' "
                + "requires a registered aggregate-owned slot.");
        }

        _ = _canonicalWriters.TryAdd((domain, projectionName, slot), 0);
    }

    /// <inheritdoc/>
    public bool TryGetKind(string projectionName, string slot, out ProjectionReadModelSlotKind kind) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);
        if (_slots.TryGetValue((projectionName, slot), out ProjectionReadModelSlotDeclaration? declaration)) {
            kind = declaration.Kind;
            return true;
        }

        kind = default;
        return false;
    }

    /// <inheritdoc/>
    public bool DeclaresCanonicalWriter(string domain, string projectionName, string slot) {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);
        return _canonicalWriters.ContainsKey((domain, projectionName, slot));
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAggregateOwnedSlots(string projectionName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);
        return [.. _slots
            .Where(entry => entry.Value.Kind == ProjectionReadModelSlotKind.AggregateOwned
                && string.Equals(entry.Key.Projection, projectionName, StringComparison.Ordinal))
            .Select(entry => entry.Key.Slot)
            .OrderBy(static slot => slot, StringComparer.Ordinal)];
    }
}
