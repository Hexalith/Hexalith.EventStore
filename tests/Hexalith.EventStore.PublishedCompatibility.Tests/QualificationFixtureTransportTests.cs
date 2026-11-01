using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Drives the actual fixture producer through a real bounded HTTP connection and independent byte observation.</summary>
public sealed class QualificationFixtureTransportTests
{
    [Fact]
    public async Task ActualFixtureProducerRetainsSafeOperationsAndTheExactObservedRequestAsync()
    {
        await using var runtime = new QualificationRuntime();
        await using var cluster = new PublishedCluster(runtime, []);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        typeof(PublishedCluster).GetField("_hostPort", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cluster, ((IPEndPoint)listener.LocalEndpoint).Port);
        const string actor = "tenant-a:counter:owned", secret = "actual-fixture-private-value-not-retained";
        object operations = new[] { new { operation = "upsert", key = actor + ":metadata", value = new
        {
            retainedFloor = 14, metadataVersion = 987, serializationFormat = "protected+json", eventTypeName = "Unknown.Event",
            payload = Convert.ToBase64String("invalid-json"u8.ToArray()), credential = secret,
        } } };
        async Task<byte[]> ServeAsync()
        {
            using var connection = await listener.AcceptTcpClientAsync(deadline.Token);
            await using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, false, leaveOpen: true);
            string line = (await reader.ReadLineAsync(deadline.Token))!;
            Uri.UnescapeDataString(line.Split(' ')[1]).ShouldBe("/fixture/" + actor);
            int length = 0;
            while ((line = (await reader.ReadLineAsync(deadline.Token))!).Length != 0)
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(line[15..], System.Globalization.CultureInfo.InvariantCulture);
            length.ShouldBeInRange(1, 4096);
            char[] input = new char[length]; (await reader.ReadBlockAsync(input, deadline.Token)).ShouldBe(length);
            await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(), deadline.Token);
            await stream.FlushAsync(deadline.Token);
            return Encoding.UTF8.GetBytes(input);
        }
        Task<byte[]> server = ServeAsync();
        var observations = new List<ScenarioObservation>();
        await PublishedScenarioTests.ObserveAsync(observations, "control", "actual-fixture-capture", async assertions =>
        {
            await (Task)typeof(PublishedCluster).GetMethod("FixtureAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cluster, [actor, operations])!;
            assertions.Check(true, "actual-fixture-post-completed");
            return "compatible";
        });
        var observation = observations.ShouldHaveSingleItem(); observation.Execution.ShouldBe("passed");
        byte[] actual = await server;
        var post = JsonSerializer.Deserialize<JsonElement>(observation.Measurements!["fixture-http-post"]);
        post.GetProperty("requestHash").GetString().ShouldBe(QualificationRuntime.Hash(actual));
        JsonElement.DeepEquals(post.GetProperty("request"), HistoricalHttpOperationCapture.Request(JsonSerializer.Deserialize<JsonElement>(actual))).ShouldBeTrue();
        var captured = post.GetProperty("request").GetProperty("fixture")[0].Deserialize<HistoricalFixtureOperation>()!;
        captured.RetainedFloor.ShouldBe(14); captured.MetadataVersion.ShouldBe(987); captured.SerializationFormat.ShouldBe("protected+json");
        JsonSerializer.Serialize(post).ShouldNotContain(secret);
        HistoricalMatrixTransportContract.Validate(observation, runtime.HttpExchanges);
        var altered = post.GetProperty("request").GetRawText().Replace("987", "986", StringComparison.Ordinal);
        var records = new Dictionary<string, string>(observation.Measurements);
        var node = System.Text.Json.Nodes.JsonNode.Parse(records["fixture-http-post"])!;
        node["request"] = System.Text.Json.Nodes.JsonNode.Parse(altered); records["fixture-http-post"] = node.ToJsonString();
        Should.Throw<InvalidOperationException>(() => HistoricalMatrixTransportContract.Validate(observation with { Measurements = records }, runtime.HttpExchanges))
            .Message.ShouldContain("independent actual transport byte/semantic commitments");
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("value")]
    [InlineData("count")]
    public void UnknownFixtureShapesRemainExplicitlyUnsupported(string fault)
    {
        object item = new { operation = fault == "operation" ? "replace" : "upsert", key = "tenant-a:counter:owned:metadata",
            value = fault == "value" ? (object)"unknown" : new { retainedFloor = 14 } };
        var projection = HistoricalHttpOperationCapture.Request(Enumerable.Repeat(item, fault == "count" ? 33 : 1).ToArray());
        projection.GetProperty("unsupported").GetBoolean().ShouldBeTrue();
    }
}
