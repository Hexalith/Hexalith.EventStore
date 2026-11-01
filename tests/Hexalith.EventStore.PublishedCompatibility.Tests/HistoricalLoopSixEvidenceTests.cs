using System.Text.Json;
using System.Text.Json.Nodes;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Case-specific finite corruption coordinates remain required without inventing historical captures.</summary>
public sealed class HistoricalLoopSixEvidenceTests
{
    [Theory]
    [InlineData("floor-0", "RetainedFloor", true)]
    [InlineData("floor-0", "RetainedFloor", false)]
    [InlineData("floor-14", "RetainedFloor", true)]
    [InlineData("floor-14", "RetainedFloor", false)]
    [InlineData("version-987", "MetadataVersion", true)]
    [InlineData("version-987", "MetadataVersion", false)]
    [InlineData("protected-mismatch", "SerializationFormat", true)]
    [InlineData("protected-mismatch", "SerializationFormat", false)]
    [InlineData("unknown-type", "EventTypeNameSha256", true)]
    [InlineData("unknown-type", "EventTypeNameSha256", false)]
    [InlineData("unreadable", "PayloadSha256", true)]
    [InlineData("unreadable", "PayloadSha256", false)]
    public void RetainedFixtureMutationMustDescribeItsRequiredCase(string corruption, string field, bool valid)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create();
        var observation = fixture.Observations.Single(item => item.Case == "3.128.2-Release-" + corruption + "-tenant-a");
        var records = (Dictionary<string, string>)observation.Measurements!;
        if (!valid)
        {
            var post = JsonNode.Parse(records["fixture-http-post"])!;
            post["request"]!["fixture"]![0]![field] = field is "RetainedFloor" or "MetadataVersion" ? JsonValue.Create(1) : JsonValue.Create("different");
            records["fixture-http-post"] = post.ToJsonString();
        }
        void Check() => HistoricalMatrixFixtureInputContract.Validate(observation, records);
        if (valid) Check();
        else Should.Throw<InvalidOperationException>(Check).Message.ShouldBe("The actual fixture mutation differs from its required corruption case.");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("key")]
    [InlineData("address")]
    public void MissingOrForeignFixtureCoordinatesCannotStandInForActualMutation(string fault)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create();
        var observation = fixture.Observations.Single(item => item.Case == "3.128.2-Release-floor-0-tenant-a");
        var records = (Dictionary<string, string>)observation.Measurements!;
        var post = JsonNode.Parse(records["fixture-http-post"])!;
        if (fault == "missing") records.Remove("fixture-http-post");
        else
        {
            if (fault == "unknown") post["request"] = new JsonObject { ["unsupported"] = true };
            if (fault == "key") post["request"]!["fixture"]![0]!["Key"] = "tenant-a:counter:other:metadata";
            if (fault == "address") post["address"] = "http://127.0.0.1:1/fixture/foreign";
            records["fixture-http-post"] = post.ToJsonString();
        }
        Should.Throw<InvalidOperationException>(() => HistoricalMatrixFixtureInputContract.Validate(observation, records))
            .Message.ShouldContain(fault is "missing" or "unknown" ? "input was not retained" : "actor, key or owning request");
    }
}
