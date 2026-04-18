using Hexalith.EventStore.LoadTests.Scenarios;

using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;

namespace Hexalith.EventStore.LoadTests;

internal static class Program
{
    private const string DefaultBaseUrl = "http://localhost:5170";

    private static int Main(string[] args)
    {
        Uri baseAddress = ResolveBaseAddress();
        IReadOnlyCollection<string> selected = ResolveSelectedScenarios(args);

        Console.WriteLine($"Hexalith.EventStore load harness");
        Console.WriteLine($"  target:    {baseAddress}");
        Console.WriteLine($"  scenarios: {string.Join(", ", selected)}");

        ScenarioProps[] scenarios = [.. BuildScenarios(baseAddress, selected)];

        if (scenarios.Length == 0)
        {
            Console.Error.WriteLine("No scenarios selected. Set LOAD_TEST_SCENARIOS or pass scenario names as args.");
            return 1;
        }

        NodeStats stats = NBomberRunner
            .RegisterScenarios(scenarios)
            .WithTestSuite("Hexalith.EventStore.LoadTests")
            .WithTestName("perf-lab")
            .WithReportFolder("reports")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Csv, ReportFormat.Md)
            .Run();

        // Surface a non-zero exit code if any scenario failed assertions, so CI fails the job.
        return stats.AllFailCount > 0 ? 2 : 0;
    }

    private static Uri ResolveBaseAddress()
    {
        string raw = Environment.GetEnvironmentVariable("EVENTSTORE_BASE_URL")?.Trim() ?? DefaultBaseUrl;
        return new Uri(raw, UriKind.Absolute);
    }

    private static IReadOnlyCollection<string> ResolveSelectedScenarios(string[] args)
    {
        if (args.Length > 0)
        {
            return args;
        }

        string? envSelection = Environment.GetEnvironmentVariable("LOAD_TEST_SCENARIOS");
        if (!string.IsNullOrWhiteSpace(envSelection))
        {
            return envSelection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // Default selection: only the implemented scenario. Stubs are opt-in to keep noise low.
        return ["nfr7"];
    }

    private static List<ScenarioProps> BuildScenarios(Uri baseAddress, IReadOnlyCollection<string> selected)
    {
        var scenarios = new List<ScenarioProps>();
        foreach (string name in selected)
        {
            switch (name.ToLowerInvariant())
            {
                case "nfr7":
                    scenarios.Add(Nfr7CommandSubmissionScenario.Build(baseAddress));
                    break;
                case "nfr39":
                    scenarios.Add(Nfr39QueryThroughputScenario.Build(baseAddress));
                    break;
                case "nfr17":
                    scenarios.Add(Nfr17ActiveAggregatesScenario.Build(baseAddress));
                    break;
                default:
                    Console.Error.WriteLine($"Unknown scenario '{name}'. Valid: nfr7, nfr39, nfr17.");
                    break;
            }
        }

        return scenarios;
    }
}
