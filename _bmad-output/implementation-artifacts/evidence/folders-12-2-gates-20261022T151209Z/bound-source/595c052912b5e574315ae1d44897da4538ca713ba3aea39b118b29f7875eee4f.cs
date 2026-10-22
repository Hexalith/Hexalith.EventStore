namespace Hexalith.EventStore.Server.Projections;

/// <summary>Freezes separately attested replay recovery context on the original durable work.</summary>
internal static class ProjectionReplayRecoveryContext
{
    internal static IReadOnlyDictionary<string, string>? Freeze(NamedProjectionRouteCatalogEntry catalog)
    {
        KeyValuePair<string, Hexalith.EventStore.Contracts.Projections.ProjectionReplayRecoveryBinding>[] bindings =
            [.. catalog.ReplayRecoveryBindings.Where(pair => catalog.OrdinaryProjectionTypes.Contains(pair.Key, StringComparer.Ordinal))];
        return bindings.Length == 0 ? null : bindings.ToDictionary(static pair => pair.Key,
            static pair => pair.Value.BindingFingerprint, StringComparer.Ordinal);
    }

    internal static bool Matches(IReadOnlyDictionary<string, string>? left, IReadOnlyDictionary<string, string>? right)
        => left is null ? right is null : right is not null && left.Count == right.Count
            && left.All(pair => right.TryGetValue(pair.Key, out string? value) && value == pair.Value);
}
