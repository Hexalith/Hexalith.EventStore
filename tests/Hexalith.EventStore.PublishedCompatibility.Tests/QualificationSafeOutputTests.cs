using System.Text;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Real child output remains usable in memory while every persisted path retains only safe commitments.</summary>
public sealed class QualificationSafeOutputTests
{
    [Fact]
    public async Task InterruptedOriginalByteHashExcludesTheRecorderDiagnostic()
    {
        await using var runtime = new QualificationRuntime();
        await Should.ThrowAsync<TimeoutException>(() => runtime.RunAsync(new("python3", ["-c", "import time; print('observed-before-timeout',flush=True); time.sleep(120)"], runtime.Root, TimeSpan.FromMilliseconds(200))));
        var actual = runtime.Commands.Single();
        actual.OutputHash.ShouldBe(QualificationRuntime.Hash(Encoding.UTF8.GetBytes(actual.StandardOutput)));
        actual.StandardError.Contains("input/execution", StringComparison.Ordinal).ShouldBeTrue();
        var safe = HistoricalExecutionCommand.Capture(actual).Record;
        safe.GetProperty("OutputProjected").GetBoolean().ShouldBeTrue();
        safe.GetProperty("OutputHash").GetString().ShouldNotBe(safe.GetProperty("SafeOutputHash").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualChildSignedFieldsNeverEnterReceiptsLogsSummaryOrFailure(bool fail)
    {
        var runtime = new QualificationRuntime();
        string material = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        string input = JsonSerializer.Serialize(new { operation = "wire", value = material });
        const string program = "import sys,json; x=json.load(sys.stdin); v=x['value']; print('P1R_RESULT '+json.dumps({'value':{'identityAdmissionProof':v},'wire':v,'cursor':v,'signingKey':'[redacted]'})); sys.stderr.write(v if sys.argv[1]=='fail' else ''); sys.exit(1 if sys.argv[1]=='fail' else 0)";
        try
        {
            if (fail)
            {
                var error = await Should.ThrowAsync<InvalidOperationException>(() => runtime.RequireAsync("python3", ["-c", program, "fail"], input: input));
                error.ToString().Contains(material, StringComparison.Ordinal).ShouldBeFalse();
            }
            else
            {
                string raw = await runtime.RequireAsync("python3", ["-c", program, "success"], input: input);
                raw.ShouldContain(material);
                var actual = runtime.Commands.Single();
                actual.OutputHash.ShouldBe(QualificationRuntime.Hash(Encoding.UTF8.GetBytes(actual.StandardOutput + actual.StandardError)));
            }
            string summary = await PublishedScenarioTests.WriteSummaryAsync(runtime, [], "Isolated real-child safe-output control.");
            summary.Contains(material, StringComparison.Ordinal).ShouldBeFalse();
            foreach (string path in Directory.EnumerateFiles(runtime.Root).Where(path => path.EndsWith(".json", StringComparison.Ordinal) || path.EndsWith(".log", StringComparison.Ordinal)))
                (await File.ReadAllTextAsync(path)).Contains(material, StringComparison.Ordinal).ShouldBeFalse();
            var captured = HistoricalExecutionCommand.Capture(runtime.Commands.Single());
            string safeOutput = captured.Record.GetProperty("StandardOutput").GetString()!, safeError = captured.Record.GetProperty("StandardError").GetString()!;
            captured.Record.GetProperty("SafeOutputHash").GetString().ShouldBe(QualificationRuntime.Hash(Encoding.UTF8.GetBytes(safeOutput + safeError)));
            captured.Record.GetProperty("OutputHash").GetString().ShouldNotBe(captured.Record.GetProperty("SafeOutputHash").GetString());
            var roundtrip = JsonSerializer.Deserialize<HistoricalExecutionCommand>(JsonSerializer.Serialize(captured))!;
            JsonElement.DeepEquals(captured.Record, roundtrip.Record).ShouldBeTrue();
            var value = PublishedScenarioTests.ParseProbeOutput(safeOutput);
            HistoricalSafeJson.NonemptyString(value.GetProperty("value").GetProperty("identityAdmissionProof")).ShouldBeTrue();
        }
        finally { await runtime.DisposeAsync(); }
    }
}
