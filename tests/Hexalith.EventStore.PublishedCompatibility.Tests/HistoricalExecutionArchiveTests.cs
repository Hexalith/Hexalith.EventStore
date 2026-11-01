using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Actual child-command safe input coordinates survive the uploaded-summary round trip.</summary>
public sealed class HistoricalExecutionArchiveTests
{
    [Theory]
    [InlineData("metadata")]
    [InlineData("wire")]
    [InlineData("cross-client")]
    public async Task ActualConsumedInputRetainsOnlySafeCoordinatesInSummaryAsync(string operation)
    {
        await using var runtime = new QualificationRuntime();
        string secret = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        object body = operation switch
        {
            "metadata" => new { operation, value = JsonSerializer.Serialize(new { currentSequence = 12, retainedFloor = 14, eTag = secret, lastModified = DateTimeOffset.UnixEpoch }), credential = "[redacted]" },
            "wire" => new { operation, kind = "query", serializer = "json", value = JsonSerializer.Serialize(new { tenantId = "tenant-a", identityAdmissionProof = secret, cursor = secret }) },
            _ => (object)new { operation, action = "common", sidecar = "http://127.0.0.1:3500", actor = "tenant-a:counter:owned", credential = "[redacted]" },
        };
        string input = JsonSerializer.Serialize(body);
        await runtime.RequireAsync("python3", ["-c", "import sys; data=sys.stdin.read(); print(len(data))"], timeout: TimeSpan.FromSeconds(10),
            environment: new Dictionary<string, string> { ["P1R_PRIVATE_INPUT"] = secret }, input: input);
        var observations = new List<ScenarioObservation>();
        string text = await PublishedScenarioTests.WriteSummaryAsync(runtime, observations, "Nonqualifying isolated command-capture control.");
        text.ShouldNotContain(secret);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(runtime.Root, "summary.json")));
        var restored = document.RootElement.GetProperty("executionCommands").Deserialize<HistoricalExecutionCommand[]>()!.ShouldHaveSingleItem();
        JsonElement.DeepEquals(JsonSerializer.SerializeToElement(restored), JsonSerializer.SerializeToElement(HistoricalExecutionCommand.Capture(runtime.Commands.Single()))).ShouldBeTrue();
        restored.Record.GetProperty("Command").GetProperty("InputHash").GetString().ShouldBe(QualificationRuntime.Hash(System.Text.Encoding.UTF8.GetBytes(input)));
        restored.Record.GetProperty("Command").TryGetProperty("Input", out _).ShouldBeFalse();
        restored.Record.GetProperty("Command").TryGetProperty("Environment", out _).ShouldBeFalse();
        restored.Operation.ShouldBe(operation);
        if (operation == "metadata") { restored.Metadata!.Floor.ShouldBe(14); restored.Metadata.Head.ShouldBe(12); }
        if (operation == "wire") { restored.Wire!.Fields!.ContainsKey("identityAdmissionProof").ShouldBeTrue(); restored.Kind.ShouldBe("query"); }
        if (operation == "cross-client") { restored.CrossClient!.Actor.ShouldBe("tenant-a:counter:owned"); restored.Action.ShouldBe("common"); }
        document.RootElement.GetProperty("qualified").GetBoolean().ShouldBeFalse();
    }
}
