namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Separates successful measurement from compatibility and retains unavailable operations.</summary>
internal sealed record ScenarioObservation(string Scenario, string Case, string Execution, string Compatibility,
    int Assertions, IReadOnlyList<string> Failures, string? Error = null)
{
    /// <summary>Gets whether this required observation can contribute to technical qualification.</summary>
    internal bool Passing => Execution == "passed" && Compatibility == "compatible" && Assertions > 0 && Failures.Count == 0;
}
