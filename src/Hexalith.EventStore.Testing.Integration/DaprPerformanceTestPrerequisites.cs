namespace Hexalith.EventStore.Testing.Integration;

/// <summary>
/// Discovery-time prerequisite check for DAPR-backed performance tests. Performance tests are opt-in
/// (they are slow and resource-intensive) and consume the same required runner handoff.
/// </summary>
public static class DaprPerformanceTestPrerequisites {
    /// <summary>Environment variable that enables DAPR-backed performance tests when set to <c>1</c>.</summary>
    public const string EnablePerformanceTestsVariable = "HEXALITH_EVENTSTORE_RUN_PERFORMANCE_TESTS";

    /// <summary>
    /// Gets a value indicating whether DAPR-backed performance tests should run.
    /// </summary>
    public static bool IsAvailable
        => Environment.GetEnvironmentVariable(EnablePerformanceTestsVariable) == "1";

    /// <summary>
    /// Gets the skip reason used when performance tests are not enabled.
    /// </summary>
    public static string SkipReason
        => $"DAPR performance tests are disabled. Set {EnablePerformanceTestsVariable}=1 to run them.";
}
