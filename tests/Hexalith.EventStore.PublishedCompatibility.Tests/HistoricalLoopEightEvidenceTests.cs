using System.Text.Json;
using System.Text.Json.Nodes;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Checks the repaired independent archived evidence, without converting old captures into current receipts.</summary>
public sealed class HistoricalLoopEightEvidenceTests
{
    [Fact]
    public void CompleteSyntheticCaptureRetainsAllCurrentIndependentCoordinates() => HistoricalMatrixSyntheticFixture.Create().Validate();

    [Theory]
    [InlineData("missing")]
    [InlineData("changed")]
    [InlineData("dropped")]
    public void UploadedCommandsMustEqualTheIndependentActualCapture(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        if (mutation == "missing") fixture.Summary.Remove("executionCommands");
        else if (mutation == "dropped") fixture.Summary["executionCommands"]!.AsArray().RemoveAt(0);
        else fixture.Summary["executionCommands"]![0]!["Record"]!["OutputHash"] = new string('0', 64);
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain("executionCommands ledger");
    }

    [Fact]
    public void SecurityCaptureUsesItsActualFirstHostAcrossTheRestart()
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        var row = fixture.Observations.First(row => row.Measurements!.ContainsKey("capability-security-capture"));
        var records = (Dictionary<string, string>)row.Measurements!;
        var profiles = records.Where(pair => pair.Key.Split('#')[0] == "runtime-profile").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        var capture = JsonNode.Parse(records["capability-security-capture"])!;
        capture["pid"]!.GetValue<int>().ShouldBe(profiles[0].GetProperty("hostProcessId").GetInt32());
        capture["pid"]!.GetValue<int>().ShouldNotBe(profiles[^1].GetProperty("hostProcessId").GetInt32());
        HistoricalMatrixSecurityContract.Validate(row, records, fixture.Commands.Select(HistoricalExecutionCommand.Capture).ToArray());
        capture["profileId"] = profiles[^1].GetProperty("profileId").GetString(); records["capability-security-capture"] = capture.ToJsonString();
        Should.Throw<InvalidOperationException>(() => HistoricalMatrixSecurityContract.Validate(row, records, fixture.Commands.Select(HistoricalExecutionCommand.Capture).ToArray()))
            .Message.ShouldContain("contemporaneous Host profile");
    }

    [Theory]
    [InlineData("actual-writer-admission-result")]
    [InlineData("capability-security-observations")]
    [InlineData("actual-execute-result")]
    public void ClaimedResultsMustMatchTheirActualEndpointResponse(string key)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        var row = fixture.Observations.First(row => row.Measurements!.ContainsKey(key)); var records = (Dictionary<string, string>)row.Measurements!;
        var claim = JsonNode.Parse(records[key])!;
        if (key == "actual-execute-result") claim["result"]!["count"] = 999;
        else if (key == "actual-writer-admission-result") claim["protectionCalls"] = 999;
        else claim["sequence"] = 999;
        records[key] = claim.ToJsonString();
        Should.Throw<InvalidOperationException>(() => HistoricalMatrixOperationResultContract.Validate(row, records)).Message.ShouldContain("response commitment");
    }

    [Fact]
    public void ReplayRequiresItsActualSafeExecutionOutcome()
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        var row = fixture.Observations.First(row => row.Scenario == "full-replay"); var records = (Dictionary<string, string>)row.Measurements!;
        records.Remove("actual-execute-result");
        Should.Throw<InvalidOperationException>(() => HistoricalMatrixOperationResultContract.Validate(row, records)).Message.ShouldContain("result was not retained");
    }
}
