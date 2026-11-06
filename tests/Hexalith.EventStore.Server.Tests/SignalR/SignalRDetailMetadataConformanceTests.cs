using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.SignalR;
using Hexalith.EventStore.SignalRHub;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using ProjectionChangedDetail = Hexalith.EventStore.Client.Projections.ProjectionChangedDetail;

namespace Hexalith.EventStore.Server.Tests.SignalR;

/// <summary>Proves Story 2.16's NFR5 boundaries and support-safe detail logging.</summary>
public class SignalRDetailMetadataConformanceTests
{
    [Fact]
    public void DefaultOptionsMatchSignalRContractCeilings()
    {
        var notifierOptions = new ProjectionChangeNotifierOptions();
        var signalROptions = new SignalROptions();

        notifierOptions.MaxDetailMetadataEntries.ShouldBe(16);
        notifierOptions.MaxDetailMetadataBytes.ShouldBe(2_048);
        signalROptions.MaxDetailMetadataEntries.ShouldBe(notifierOptions.MaxDetailMetadataEntries);
        signalROptions.MaxDetailMetadataBytes.ShouldBe(notifierOptions.MaxDetailMetadataBytes);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    public async Task DefaultEntryBoundaryPreservesOpaqueKeysAndClipsOrdinalPrefix(int entryCount)
    {
        var (broadcaster, client, logger) = CreateBroadcaster();
        // Insert in reverse order: clipping follows ordinal keys, not insertion order or a key allow-list.
        var metadata = Enumerable.Range(0, entryCount).Reverse()
            .ToDictionary(index => $"opaque:{index:D2}/?\"é", index => $"value-{index}", StringComparer.Ordinal);

        await broadcaster.BroadcastChangedAsync(new ProjectionChangedDetail("order-list", "acme", "scope", metadata))
            .ConfigureAwait(true);

        IReadOnlyDictionary<string, string> sent = SentMetadata(client);
        sent.Count.ShouldBe(16);
        OrdinalEntries(sent).ShouldBe(OrdinalEntries(metadata).Take(16).ToArray());
        JsonSerializer.SerializeToUtf8Bytes(sent).Length.ShouldBeLessThanOrEqualTo(2_048);
        metadata.Count.ShouldBe(entryCount);
        ShouldLogClip(logger, originalCount: entryCount, boundedCount: 16);
    }

    [Theory]
    [InlineData(2_048, 16)]
    [InlineData(2_049, 15)]
    public async Task DefaultByteBoundaryIncludesEscapingAndClipsWholeEntries(int serializedBytes, int retainedEntries)
    {
        var (broadcaster, client, logger) = CreateBroadcaster();
        var metadata = Enumerable.Range(0, 16)
            .ToDictionary(index => $"opaque:{index:D2}/?\"é", _ => "value", StringComparer.Ordinal);
        string lastKey = metadata.Keys.Order(StringComparer.Ordinal).Last();
        metadata[lastKey] += new string('x', serializedBytes - JsonSerializer.SerializeToUtf8Bytes(metadata).Length);
        JsonSerializer.SerializeToUtf8Bytes(metadata).Length.ShouldBe(serializedBytes);

        await broadcaster.BroadcastChangedAsync(new ProjectionChangedDetail("order-list", "acme", "scope", metadata))
            .ConfigureAwait(true);

        IReadOnlyDictionary<string, string> sent = SentMetadata(client);
        sent.Count.ShouldBe(retainedEntries);
        OrdinalEntries(sent).ShouldBe(OrdinalEntries(metadata).Take(retainedEntries).ToArray());
        JsonSerializer.SerializeToUtf8Bytes(sent).Length.ShouldBeLessThanOrEqualTo(2_048);
        if (serializedBytes == 2_048)
        {
            JsonSerializer.SerializeToUtf8Bytes(sent).Length.ShouldBe(2_048);
        }

        ShouldLogClip(logger, originalCount: 16, boundedCount: retainedEntries);
    }

    [Fact]
    public async Task EntryClippingUsesOrdinalRatherThanLinguisticKeyOrder()
    {
        var (broadcaster, client, _) = CreateBroadcaster();
        var metadata = Enumerable.Range(0, 15).ToDictionary(index => $"a{index:D2}", _ => "value", StringComparer.Ordinal);
        metadata["é"] = "accent-value";
        metadata["z"] = "ascii-value";

        await broadcaster.BroadcastChangedAsync(new ProjectionChangedDetail("order-list", "acme", "scope", metadata))
            .ConfigureAwait(true);

        IReadOnlyDictionary<string, string> sent = SentMetadata(client);
        sent.Count.ShouldBe(16);
        sent["z"].ShouldBe("ascii-value");
        sent.ContainsKey("é").ShouldBeFalse();
    }

    [Fact]
    public async Task OversizedMiddleEntryEndsPrefixEvenWhenLaterEntryWouldFit()
    {
        var (broadcaster, client, _) = CreateBroadcaster();
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["c"] = "small",
            ["b"] = new string('x', 2_048),
            ["a"] = "small",
        };

        await broadcaster.BroadcastChangedAsync(new ProjectionChangedDetail("order-list", "acme", "scope", metadata))
            .ConfigureAwait(true);

        SentMetadata(client).ToArray().ShouldBe(new[] { new KeyValuePair<string, string>("a", "small") });
    }

    [Fact]
    public async Task ClientReceiptLogsExcludeMetadataFromRenderedAndStructuredState()
    {
        var logger = new SignalRDetailLogCapture<EventStoreSignalRClient>();
        await using var client = new EventStoreSignalRClient(
            new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" }, logger);
        string metadataValue = $"metadata-value-{Guid.NewGuid():N}";
        Hexalith.EventStore.SignalR.ProjectionChangedDetail? received = null;
        await client.SubscribeDetailAsync("order-list", "acme", "scope", detail => received = detail, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        MethodInfo? receive = typeof(EventStoreSignalRClient).GetMethod("OnProjectionChangedDetail", BindingFlags.Instance | BindingFlags.NonPublic);
        receive.ShouldNotBeNull();

        _ = receive.Invoke(client, ["order-list", "acme", "scope", new Dictionary<string, string> { ["opaque:é/?"] = metadataValue }]);

        received.ShouldNotBeNull();
        received.Metadata["opaque:é/?"].ShouldBe(metadataValue);
        var receipt = logger.Entries.Single(entry => entry.EventId.Id == 2091);
        receipt.Level.ShouldBe(LogLevel.Information);
        receipt.Message.ShouldContain("MetadataCount: 1");
        foreach (var entry in logger.Entries.Where(entry => entry.Level > LogLevel.Debug))
        {
            entry.Message.ShouldNotContain(metadataValue);
            entry.State.ShouldNotBeEmpty();
            entry.State.ShouldAllBe(field => field.Value == null || !field.Value.ToString()!.Contains(metadataValue, StringComparison.Ordinal));
            entry.Exception.ShouldBeNull();
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DetailLogsNeverExposeValuesInStateOrExceptionsAboveDebug(bool failDelivery, bool clipMetadata)
    {
        var (broadcaster, client, logger) = CreateBroadcaster();
        string metadataValue = $"metadata-value-{Guid.NewGuid():N}";
        var metadata = Enumerable.Range(0, clipMetadata ? 17 : 1)
            .ToDictionary(index => $"opaque:{index:D2}", _ => metadataValue, StringComparer.Ordinal);
        if (failDelivery)
        {
            var exception = new InvalidOperationException($"Transport rejected {metadataValue}", new ArgumentException(metadataValue));
            exception.Data["detail"] = metadataValue;
            _ = client.ProjectionChangedDetail("order-list", "acme", "scope", Arg.Any<IReadOnlyDictionary<string, string>>())
                .Returns(Task.FromException(exception));
        }

        await Should.NotThrowAsync(() => broadcaster.BroadcastChangedAsync(
            new ProjectionChangedDetail("order-list", "acme", "scope", metadata))).ConfigureAwait(true);

        var logs = logger.Entries
            .Where(entry => entry.Level > LogLevel.Debug)
            .ToArray();
        logs.Length.ShouldBe(clipMetadata ? 3 : 2);
        foreach (var entry in logs)
        {
            entry.Message.ShouldNotContain(metadataValue);
            entry.State.ShouldNotBeEmpty();
            entry.State.ShouldAllBe(field => field.Value == null || !field.Value.ToString()!.Contains(metadataValue, StringComparison.Ordinal));
            // Logging providers render exceptions independently of the message and structured state.
            entry.Exception.ShouldBeNull();
        }

        int outcomeEvent = failDelivery ? 1095 : 1094;
        var outcome = logs.Single(entry => entry.EventId.Id == outcomeEvent);
        if (failDelivery)
        {
            outcome.Level.ShouldBe(LogLevel.Warning);
            outcome.Message.ShouldContain(nameof(InvalidOperationException));
            outcome.Message.ShouldContain("FailOpenFailure");
            // The call passes six positional strings; pin each preserved field by key so a swap cannot pass.
            Field(outcome.State, "ProjectionType").ShouldBe("order-list");
            Field(outcome.State, "TenantId").ShouldBe("acme");
            Field(outcome.State, "GroupName").ShouldBe("order-list:acme:scope");
            Field(outcome.State, "Result").ShouldBe("FailOpenFailure");
            Field(outcome.State, "ExceptionType").ShouldBe(nameof(InvalidOperationException));
            _ = Field(outcome.State, "ElapsedMilliseconds").ShouldBeOfType<double>();
        }

        ShouldLogClip(logger, originalCount: metadata.Count, boundedCount: clipMetadata ? 16 : 1);
        SentMetadata(client).Count.ShouldBe(clipMetadata ? 16 : 1);
    }

    private static (SignalRProjectionChangedBroadcaster Broadcaster, IProjectionChangedClient Client, SignalRDetailLogCapture<SignalRProjectionChangedBroadcaster> Logger) CreateBroadcaster()
    {
        IProjectionChangedClient client = Substitute.For<IProjectionChangedClient>();
        _ = client.ProjectionChangedDetail("order-list", "acme", "scope", Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(Task.CompletedTask);
        IHubClients<IProjectionChangedClient> clients = Substitute.For<IHubClients<IProjectionChangedClient>>();
        _ = clients.Group("order-list:acme:scope").Returns(client);
        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hub = Substitute.For<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
        _ = hub.Clients.Returns(clients);
        var logger = new SignalRDetailLogCapture<SignalRProjectionChangedBroadcaster>();
        return (new SignalRProjectionChangedBroadcaster(hub, logger), client, logger);
    }

    private static IReadOnlyDictionary<string, string> SentMetadata(IProjectionChangedClient client)
        => client.ReceivedCalls().Single(call => call.GetMethodInfo().Name == nameof(IProjectionChangedClient.ProjectionChangedDetail))
            .GetArguments()[3].ShouldBeAssignableTo<IReadOnlyDictionary<string, string>>()!;

    // The contract fixes which entries are retained, not the enumeration order of the returned map.
    private static KeyValuePair<string, string>[] OrdinalEntries(IEnumerable<KeyValuePair<string, string>> metadata)
        => [.. metadata.OrderBy(entry => entry.Key, StringComparer.Ordinal)];

    private static object? Field(KeyValuePair<string, object?>[] state, string name)
        => state.Single(field => field.Key == name).Value;

    private static void ShouldLogClip(SignalRDetailLogCapture<SignalRProjectionChangedBroadcaster> logger, int originalCount, int boundedCount)
    {
        var clips = logger.Entries.Where(entry => entry.EventId.Id == 1096).ToArray();
        if (originalCount == boundedCount)
        {
            clips.ShouldBeEmpty();
            return;
        }

        var clip = clips.ShouldHaveSingleItem();
        clip.Level.ShouldBe(LogLevel.Warning);
        Field(clip.State, "OriginalCount").ShouldBe(originalCount);
        Field(clip.State, "BoundedCount").ShouldBe(boundedCount);
    }
}
