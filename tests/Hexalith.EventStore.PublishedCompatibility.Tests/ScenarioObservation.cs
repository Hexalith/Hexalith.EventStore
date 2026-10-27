namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Separates successful measurement from compatibility and retains unavailable operations.</summary>
/// <param name="Scenario">The required scenario family.</param>
/// <param name="Case">The exact executed case.</param>
/// <param name="Execution">The measured execution outcome.</param>
/// <param name="Compatibility">The independent compatibility disposition.</param>
/// <param name="Assertions">The executed assertion count.</param>
/// <param name="Failures">The failed assertion identifiers.</param>
/// <param name="Error">The retained execution exception.</param>
/// <param name="Measurements">The concrete command, fixture, and inventory observations.</param>
/// <param name="Started">The case start instant.</param>
/// <param name="Ended">The case completion instant.</param>
internal sealed record ScenarioObservation(string Scenario, string Case, string Execution, string Compatibility,
    int Assertions, IReadOnlyList<string> Failures, string? Error = null, IReadOnlyDictionary<string, string>? Measurements = null,
    DateTimeOffset? Started = null, DateTimeOffset? Ended = null)
{
    /// <summary>Gets whether this required observation can contribute to technical qualification.</summary>
    internal bool Passing => Execution == "passed" && Compatibility == "compatible" && Assertions > 0 && Failures.Count == 0
        && Started is not null && Ended is not null && Ended >= Started;
}
