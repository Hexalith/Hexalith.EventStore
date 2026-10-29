using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.ServiceDefaults.Routing;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogReadModelCancellationTests
{
    [Theory]
    [InlineData("bulk", "canceled-before")]
    [InlineData("bulk", "completed")]
    [InlineData("bulk", "canceled-after-start")]
    [InlineData("save", "canceled-before")]
    [InlineData("save", "completed")]
    [InlineData("save", "canceled-after-start")]
    [InlineData("delete", "canceled-before")]
    [InlineData("delete", "completed")]
    [InlineData("delete", "canceled-after-start")]
    [InlineData("conditional", "canceled-before")]
    [InlineData("conditional", "completed")]
    [InlineData("conditional", "canceled-after-start")]
    [InlineData("reconciliation", "canceled-before")]
    [InlineData("reconciliation", "completed")]
    [InlineData("reconciliation", "canceled-after-start")]
    [InlineData("create-conflict", "canceled-before")]
    [InlineData("create-conflict", "completed")]
    [InlineData("create-conflict", "canceled-after-start")]
    public async Task ReadModelCallsOnlyEnrollAfterLocalCancellationChecks(string operation, string scenario)
    {
        var fixture = new RouteCatalogTestFixture(); var first = fixture.Candidate(); await fixture.ActivateAsync(first);
        using var cancellation = new CancellationTokenSource();
        using var client = Substitute.For<DaprClient>(); client.JsonSerializerOptions.Returns(JsonSerializerOptions.Web);
        byte[] original = "{\"value\":1}"u8.ToArray(); byte[]? persisted = original.ToArray();
        int calls = 0, reads = 0, writes = 0;
        var value = new Dictionary<string, int> { ["value"] = 2 };
        void RemoteCompletion()
        {
            if (scenario != "canceled-after-start") return;
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        }
        client.SaveStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, int>>(), Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            calls++; writes++; persisted = JsonSerializer.SerializeToUtf8Bytes(call.Arg<Dictionary<string, int>>());
            RemoteCompletion(); return Task.CompletedTask;
        });
        client.TrySaveStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, int>>(), Arg.Any<string>(), Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            calls++;
            if (operation == "reconciliation")
            {
                // A completed no-write conflict is quiescent. Cancellation here precedes only the follow-up read RPC.
                if (scenario == "canceled-before") cancellation.Cancel();
                return Task.FromResult(false);
            }
            writes++; persisted = JsonSerializer.SerializeToUtf8Bytes(call.Arg<Dictionary<string, int>>());
            RemoteCompletion(); return Task.FromResult(true);
        });
        client.TryDeleteStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            calls++; writes++; persisted = null; RemoteCompletion(); return Task.FromResult(true);
        });
        client.GetBulkStateAsync<JsonElement>(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<int?>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            calls++; reads++; RemoteCompletion();
            return Task.FromResult<IReadOnlyList<BulkStateItem<JsonElement>>>([new("detail", JsonSerializer.Deserialize<JsonElement>(persisted!), "etag-1")]);
        });
        client.GetByteStateAndETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConsistencyMode?>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            calls++; reads++; RemoteCompletion();
            return Task.FromResult<(ReadOnlyMemory<byte>, string)>((persisted!, "etag-1"));
        });
        if (scenario == "canceled-before" && operation != "reconciliation") cancellation.Cancel();
        var store = new DaprReadModelStore(client);
        async Task Invoke()
        {
            switch (operation)
            {
                case "bulk":
                    var items = await store.GetManyAsync<Dictionary<string, int>>("statestore", ["detail"], 1, cancellation.Token).ConfigureAwait(false);
                    items.ShouldHaveSingleItem().Value!["value"].ShouldBe(1); break;
                case "save": await store.SaveAsync("statestore", "detail", value, cancellation.Token).ConfigureAwait(false); break;
                case "delete": (await store.TryEraseAsync("statestore", "detail", "etag-1", cancellation.Token).ConfigureAwait(false)).ShouldBeTrue(); break;
                case "conditional": (await store.TrySaveAsync("statestore", "detail", value, "etag-1", cancellation.Token).ConfigureAwait(false)).ShouldBeTrue(); break;
                case "reconciliation": (await store.TrySaveAsync("statestore", "detail", value, "", cancellation.Token).ConfigureAwait(false)).ShouldBeFalse(); break;
                case "create-conflict": (await DaprReadModelCreateConflict.ExistsAsync(client, "statestore", "detail", cancellation.Token).ConfigureAwait(false)).ShouldBeTrue(); break;
                default: throw new InvalidOperationException("Unknown control operation.");
            }
        }
        await using (var lease = new RouteCatalogLeaseReleaser(await fixture.Authorities[0].AcquireAsync()))
        {
            if (scenario == "completed") await Invoke();
            else await Should.ThrowAsync<OperationCanceledException>(Invoke);
        }
        bool retained = scenario == "canceled-after-start";
        if (scenario == "canceled-before")
        {
            calls.ShouldBe(operation == "reconciliation" ? 1 : 0); reads.ShouldBe(0); writes.ShouldBe(0);
            persisted.ShouldBe(original);
        }
        else
        {
            calls.ShouldBe(operation == "reconciliation" ? 2 : 1);
            if (operation is "save" or "conditional")
                JsonSerializer.Deserialize<Dictionary<string, int>>(persisted!)!["value"].ShouldBe(2);
            else if (operation == "delete") persisted.ShouldBeNull();
            else persisted.ShouldBe(original);
        }
        (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(retained ? 1 : 0);
        var next = fixture.Candidate(2, first.RootDigest); await fixture.Authorities[0].PrepareAsync(next);
        if (retained) await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(next.AttemptId));
        else await fixture.Authorities[0].ReadyAsync(next.AttemptId);
    }
}
