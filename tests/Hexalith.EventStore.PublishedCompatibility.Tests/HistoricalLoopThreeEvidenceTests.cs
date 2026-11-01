using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Coherent independent review3 counterfactuals against the actual retained producer relationships.</summary>
public sealed class HistoricalLoopThreeEvidenceTests
{
    [Theory]
    [InlineData("empty-value")]
    [InlineData("preserved-field")]
    [InlineData("historical-deficit")]
    [InlineData("first-input")]
    [InlineData("second-input")]
    [InlineData("third-input")]
    public void WireHopsRequirePreservedFieldsDeficitsAndActualInputChain(string mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int row = fixture.Observations.FindIndex(item => item.Case == "3.128.2-package-Probe-to-3.70.1-package-Probe-json" && item.Scenario == "query-wire");
        int hop = mutation == "first-input" ? 0 : mutation == "second-input" ? 1 : 2;
        RewriteProbe(fixture, row, hop, mutation.EndsWith("input", StringComparison.Ordinal) ? null : output =>
        {
            if (mutation == "empty-value") output["value"] = new JsonObject();
            else output["value"]![mutation == "preserved-field" ? "tenantId" : "originalActorId"] = "foreign";
        }, mutation.EndsWith("input", StringComparison.Ordinal) ? input =>
        {
            if (hop == 0) { var value = JsonNode.Parse(input["value"]!.GetValue<string>())!; value["tenantId"] = "foreign"; input["value"] = value.ToJsonString(); }
            else input["value"] = "different-actual-wire";
        } : null);
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    [Theory]
    [InlineData("verified")]
    [InlineData("principalsBound")]
    [InlineData("missingRefused")]
    [InlineData("forgedRefused")]
    [InlineData("foreignTenantRefused")]
    [InlineData("proof")]
    [InlineData("principal")]
    public void ActualSignedQueryRequiresTheRealScopeProofAndAllAdmissionOutcomes(string field)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int row = fixture.Observations.FindIndex(item => item.Case == "real-signed-admission-and-refusals");
        RewriteProbe(fixture, row, 0, result =>
        {
            if (field == "proof") result["value"]!["identityAdmissionProof"] = "";
            else if (field == "principal") result["value"]!["originalActorId"] = "foreign";
            else result[field] = false;
        });
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    [Theory]
    [InlineData("decoded")]
    [InlineData("changed")]
    [InlineData("foreignCaller")]
    [InlineData("foreignTenant")]
    [InlineData("foreignDomain")]
    [InlineData("changedFilter")]
    [InlineData("changedQuery")]
    [InlineData("tamperDenied")]
    [InlineData("position")]
    [InlineData("unknownDenied")]
    [InlineData("supported")]
    [InlineData("legacy")]
    public void ActualCursorRequiresAllScopeChecksAndExactHistoricalIncapacity(string field)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int row = fixture.Observations.FindIndex(item => item.Case == (field == "legacy" ? "3.70.1" : "3.128.2") + "-package-Probe-cursor");
        RewriteProbe(fixture, row, 0, result =>
        {
            if (field == "legacy") result["protectedAccess"] = true;
            else if (field == "position") result[field] = "0";
            else if (field == "unknownDenied") result[field] = 0;
            else result[field] = false;
        });
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    [Theory]
    [InlineData("eTag")]
    [InlineData("lastModified")]
    public void MetadataOutputMustPreserveItsGenuineETagAndTimestampInput(string field)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int row = fixture.Observations.FindIndex(item => item.Scenario == "metadata-read" && item.Case == "3.128.2-floor-5");
        RewriteProbe(fixture, row, 0, null, input =>
        {
            var value = JsonNode.Parse(input["value"]!.GetValue<string>())!;
            value[field] = field == "eTag" ? "foreign-etag" : "2026-01-01T00:00:00Z";
            input["value"] = value.ToJsonString();
        });
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    [Theory]
    [InlineData("host")]
    [InlineData("actor")]
    public void ReaderTimingAndBodyCannotBeDetachedFromItsActualHostAndFullActor(string field)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Observations.FindIndex(item => item.Case == "3.128.2-Release-floor-0-tenant-a");
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        string key = records.Keys.Single(key => key.StartsWith("http-get:", StringComparison.Ordinal) && key.Contains("/reader/", StringComparison.Ordinal));
        string old = key["http-get:".Length..];
        string changed = field == "host" ? "http://127.0.0.1:1" + new Uri(old).AbsolutePath : new Uri(old).GetLeftPart(UriPartial.Authority) + "/reader/foreign-actor";
        records["http-get:" + changed] = records[key]; records.Remove(key);
        foreach (string timing in records.Keys.Where(key => key.Split('#')[0] == "http-get-observation").ToArray())
        { var value = JsonNode.Parse(records[timing])!; if (value["address"]!.GetValue<string>() == old) { value["address"] = changed; records[timing] = value.ToJsonString(); } }
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    [Theory]
    [InlineData("requestHash")]
    [InlineData("responseHash")]
    public void PostRecordsRequireActualTransportBytesBeyondSemanticShape(string field)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Observations.FindIndex(item => item.Case == "Release-durably-applied-watermark-cursor");
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        var post = JsonNode.Parse(records["http-post"])!; post[field] = new string('0', 64); records["http-post"] = post.ToJsonString();
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("error")]
    [InlineData("message")]
    [InlineData("count")]
    [InlineData("sequence")]
    public void ManifestCommandCannotClaimSuccessEvenWithCoherentTransportCaptures(string field)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Observations.FindIndex(item => item.Case == "Release-registered-manifest-production-hop");
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        var data = JsonNode.Parse(records["production-manifest-command-result"])!;
        if (field == "ok") data[field] = true; else if (field is "error" or "message") data[field] = ""; else data[field] = 0;
        string body = data.ToJsonString(); records["production-manifest-command-result"] = body;
        var post = JsonNode.Parse(records["http-post"])!; post["responseHash"] = Hash(body); post["response"] = JsonSerializer.SerializeToNode(HistoricalHttpOperationCapture.Response(body));
        records["http-post"] = post.ToJsonString();
        int actual = fixture.HttpExchanges.FindIndex(item => item.Address == post["address"]!.GetValue<string>() && item.Started >= row.Started && item.Ended <= row.Ended);
        fixture.HttpExchanges[actual] = fixture.HttpExchanges[actual] with { ResponseSha256 = Hash(body), ResponseSemanticsSha256 = Hash(post["response"]!.ToJsonString()) };
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain("manifest command");
    }

    [Fact]
    public void FailedRedisSaveCannotQualifyAPreexistingDump()
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Commands.FindIndex(command => command.Command.Arguments.LastOrDefault() == "SAVE");
        var command = fixture.Commands[index]; const string output = "ERR permission denied\n";
        fixture.Commands[index] = command with { StandardOutput = output, OutputHash = Hash(output + command.StandardError) };
        fixture.RewriteSummary(); Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    private static void RewriteProbe(HistoricalMatrixSyntheticFixture fixture, int rowIndex, int hop,
        Action<JsonObject>? output = null, Action<JsonObject>? input = null)
    {
        var row = fixture.Observations[rowIndex]; var records = new Dictionary<string, string>(row.Measurements!);
        string key = hop == 0 ? "probe-command" : "probe-command#" + (hop + 1);
        var record = JsonSerializer.Deserialize<JsonElement>(records[key]);
        int index = fixture.Commands.FindIndex(command => command.Started == record.GetProperty("Started").GetDateTimeOffset());
        var command = fixture.Commands[index];
        if (output is not null)
        { var result = JsonNode.Parse(command.StandardOutput["P1R_RESULT ".Length..])!.AsObject(); output(result); string stdout = "P1R_RESULT " + result.ToJsonString(); command = command with { StandardOutput = stdout, OutputHash = Hash(stdout + command.StandardError) }; }
        if (input is not null)
        { var request = JsonNode.Parse(command.Command.Input!)!.AsObject(); input(request); command = command with { Command = command.Command with { Input = request.ToJsonString() } }; }
        fixture.Commands[index] = command;
        records[key] = JsonSerializer.Serialize(new { command.Command, command.Started, command.Ended, command.ExitCode, command.OutputHash });
        fixture.Observations[rowIndex] = row with { Measurements = records }; fixture.RewriteSummary();
    }

    private static string Hash(string value) => QualificationRuntime.Hash(Encoding.UTF8.GetBytes(value));
}
