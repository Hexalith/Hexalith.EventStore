using NBomber.Contracts.Stats;

namespace Hexalith.EventStore.LoadTests;

/// <summary>
/// Classifies NBomber results for the load-harness process exit code.
/// </summary>
internal static class LoadTestExitCode
{
    /// <summary>
    /// Returns a failure exit code when any scenario or step reports failed requests.
    /// </summary>
    /// <param name="stats">The completed NBomber node statistics.</param>
    /// <returns>Zero for success; otherwise, two.</returns>
    public static int From(NodeStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        bool hasFailures = stats.ScenarioStats.Any(
            scenario => scenario.Fail.Request.Count > 0
                || scenario.StepStats.Any(step => step.Fail.Request.Count > 0));

        return hasFailures ? 2 : 0;
    }
}
