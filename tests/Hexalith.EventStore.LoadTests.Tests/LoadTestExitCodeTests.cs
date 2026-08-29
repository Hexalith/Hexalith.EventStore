using NBomber.Contracts.Stats;

namespace Hexalith.EventStore.LoadTests.Tests;

/// <summary>
/// Tests load-harness exit-code classification.
/// </summary>
public sealed class LoadTestExitCodeTests
{
    /// <summary>
    /// Verifies successful scenario and step statistics return success.
    /// </summary>
    [Fact]
    public void FromReturnsSuccessWhenNoFailuresExist()
    {
        LoadTestExitCode.From(CreateStats(0, 0)).ShouldBe(0);
    }

    /// <summary>
    /// Verifies scenario-level failures return the load-harness failure code.
    /// </summary>
    [Fact]
    public void FromReturnsFailureWhenScenarioFailureExists()
    {
        LoadTestExitCode.From(CreateStats(1, 0)).ShouldBe(2);
    }

    /// <summary>
    /// Verifies step-level failures return the load-harness failure code.
    /// </summary>
    [Fact]
    public void FromReturnsFailureWhenStepFailureExists()
    {
        LoadTestExitCode.From(CreateStats(0, 1)).ShouldBe(2);
    }

    private static NodeStats CreateStats(int scenarioFailureCount, int stepFailureCount) =>
        new()
        {
            ScenarioStats =
            [
                new ScenarioStats
                {
                    Fail = CreateMeasurementStats(scenarioFailureCount),
                    StepStats =
                    [
                        new StepStats
                        {
                            Fail = CreateMeasurementStats(stepFailureCount),
                        },
                    ],
                },
            ],
        };

    private static MeasurementStats CreateMeasurementStats(int failureCount) =>
        new()
        {
            Request = new RequestStats(failureCount, 0, 0),
        };
}
