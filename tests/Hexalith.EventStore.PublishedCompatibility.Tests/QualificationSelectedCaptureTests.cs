using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Exercises genuine selected signed-query/wire output and restarted security/writer captures through actual published consumers.</summary>
public sealed class QualificationSelectedCaptureTests
{
    [Fact]
    public async Task ActualSelectedProbeAndRestartedSecurityWriterProduceOnlySafeBoundReceipts()
    {
        var runtime = new QualificationRuntime();
        var rows = new List<ScenarioObservation>();
        var privateValues = new List<string>();
        TestContext.Current.TestOutputHelper?.WriteLine("Invocation scratch: " + runtime.Root);
        await PublishedScenarioTests.RunFinalizedAsync(runtime, rows, async () =>
        {
            var materializer = new ConsumerMaterializer(runtime, PublishedScenarioTests.FindRepository());
            await materializer.MaterializeAsync(prerequisiteOnly: true);
            materializer.Failures.ShouldBeEmpty();
            var probe = materializer.Builds.Single(build => build.Role == "Probe");
            JsonElement signed = await PublishedScenarioTests.ProbeAsync(runtime, probe, new { operation = "signed-query" });
            signed.GetProperty("verified").GetBoolean().ShouldBeTrue();
            string admission = signed.GetProperty("value").GetProperty("identityAdmissionProof").GetString()!;
            string.IsNullOrWhiteSpace(admission).ShouldBeFalse(); privateValues.Add(admission);
            JsonElement wire = await PublishedScenarioTests.ProbeAsync(runtime, probe, new { operation = "wire", kind = "query", serializer = "json", value = signed.GetProperty("value").GetRawText() });
            privateValues.Add(wire.GetProperty("wire").GetString()!);
            var safe = PublishedScenarioTests.ParseProbeOutput(HistoricalExecutionCommand.Capture(runtime.Commands.Last()).Record.GetProperty("StandardOutput").GetString()!);
            HistoricalSafeJson.Utf8Hash(safe.GetProperty("wire")).ShouldBe(QualificationRuntime.Hash(System.Text.Encoding.UTF8.GetBytes(privateValues[^1])));
            await using (var cluster = new PublishedCluster(runtime, materializer.Builds))
            {
                await cluster.InitializeAsync();
                await cluster.ExecuteSecurityBoundariesAsync(rows);
                await cluster.ExecuteSelectedWriterBoundariesAsync(rows);
            }
            await runtime.DisposeAsync();
            rows.Count.ShouldBe(3); rows.ShouldAllBe(row => row.Passing);
            foreach (var row in rows)
            {
                var records = row.Measurements!;
                HistoricalMatrixTransportContract.Validate(row, runtime.HttpExchanges);
                HistoricalMatrixOperationResultContract.Validate(row, records);
                HistoricalMatrixSecurityContract.Validate(row, records, runtime.Commands.Select(HistoricalExecutionCommand.Capture).ToArray());
            }
            var security = rows.Single(row => row.Measurements!.ContainsKey("capability-security-capture"));
            var profiles = security.Measurements!.Where(pair => pair.Key.Split('#')[0] == "runtime-profile")
                .Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
            var captured = JsonSerializer.Deserialize<JsonElement>(security.Measurements["capability-security-capture"]);
            captured.GetProperty("pid").GetInt32().ShouldBe(profiles[0].GetProperty("hostProcessId").GetInt32());
            captured.GetProperty("pid").GetInt32().ShouldNotBe(profiles[^1].GetProperty("hostProcessId").GetInt32());
        });
        using var summary = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(runtime.Root, "summary.json")));
        summary.RootElement.GetProperty("qualified").GetBoolean().ShouldBeFalse();
        var actualCommands = runtime.Commands.Select(HistoricalExecutionCommand.Capture).ToArray();
        JsonElement.DeepEquals(summary.RootElement.GetProperty("executionCommands"), JsonSerializer.SerializeToElement(actualCommands)).ShouldBeTrue();
        foreach (string path in Directory.EnumerateFiles(runtime.Root).Where(path => path.EndsWith(".json", StringComparison.Ordinal) || path.EndsWith(".log", StringComparison.Ordinal)))
        {
            string text = await File.ReadAllTextAsync(path);
            privateValues.Any(value => text.Contains(value, StringComparison.Ordinal)).ShouldBeFalse();
        }
        runtime.CleanupFailures.ShouldBeEmpty();
    }
}
