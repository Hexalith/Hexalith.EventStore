
using System.Text.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

public class EventStoreAggregateTests {
    // --- Test Event Types ---
    private sealed class ItemAdded : IEventPayload {
        public string Name { get; init; } = string.Empty;
    }

    private sealed class ItemRemoved : IEventPayload;

    private sealed class ItemReset : IEventPayload;

    private sealed class ItemCannotBeRemoved : IRejectionEvent;

    // --- Test State ---
    private sealed class TestState {
        public int ItemCount { get; private set; }

        public string LastAdded { get; private set; } = string.Empty;

        public void Apply(ItemAdded e) {
            ItemCount++;
            LastAdded = e.Name;
        }

        public void Apply(ItemRemoved e) => ItemCount--;

        public void Apply(ItemReset e) => ItemCount = 0;
    }

    // --- Test Commands ---
    private sealed record AddItem(string Name);

    private sealed record RemoveItem;

    private sealed record ResetItems;

    private sealed record AsyncAddItem(string Name);

    private sealed record UnknownCommand;

    // --- Test Aggregate with sync Handle methods ---
    private sealed class TestAggregate : EventStoreAggregate<TestState> {
        public static DomainResult Handle(AddItem command, TestState? state)
            => DomainResult.Success(new IEventPayload[] { new ItemAdded { Name = command.Name } });

        public static DomainResult Handle(RemoveItem command, TestState? state) {
            if ((state?.ItemCount ?? 0) == 0) {
                return DomainResult.Rejection(new IRejectionEvent[] { new ItemCannotBeRemoved() });
            }

            return DomainResult.Success(new IEventPayload[] { new ItemRemoved() });
        }

        public static DomainResult Handle(ResetItems command, TestState? state) {
            if ((state?.ItemCount ?? 0) == 0) {
                return DomainResult.NoOp();
            }

            return DomainResult.Success(new IEventPayload[] { new ItemReset() });
        }
    }

    // --- Test Aggregate with async Handle methods ---
    private sealed class AsyncTestAggregate : EventStoreAggregate<TestState> {
        public static Task<DomainResult> Handle(AsyncAddItem command, TestState? state) =>
            Task.FromResult(DomainResult.Success(new IEventPayload[] { new ItemAdded { Name = command.Name } }));
    }

    // --- Test Aggregate with mixed sync/async Handle methods ---
    private sealed class MixedAggregate : EventStoreAggregate<TestState> {
        public static DomainResult Handle(AddItem command, TestState? state)
            => DomainResult.Success(new IEventPayload[] { new ItemAdded { Name = command.Name } });

        public static Task<DomainResult> Handle(AsyncAddItem command, TestState? state) =>
            Task.FromResult(DomainResult.Success(new IEventPayload[] { new ItemAdded { Name = command.Name } }));
    }

    // --- Separate aggregate type for cache independence tests ---
    private sealed class OtherState {
        public int Value { get; private set; }

        public void Apply(ItemAdded e) => Value += 100;
    }

    private sealed class OtherAggregate : EventStoreAggregate<OtherState> {
        public static DomainResult Handle(AddItem command, OtherState? state)
            => DomainResult.Success(new IEventPayload[] { new ItemAdded { Name = command.Name } });
    }

    private static CommandEnvelope CreateCommand<T>(T payload) where T : notnull {
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload);
        return new CommandEnvelope(
            TenantId: "tenant-1",
            Domain: "test",
            AggregateId: "agg-1",
            CommandType: typeof(T).Name,
            Payload: serialized,
            CorrelationId: "corr-1",
            CausationId: null,
            UserId: "user-1",
            Extensions: null);
    }

    private static CommandEnvelope CreateEmptyPayloadCommand(string commandType) =>
        new(
            TenantId: "tenant-1",
            Domain: "test",
            AggregateId: "agg-1",
            CommandType: commandType,
            Payload: [],
            CorrelationId: "corr-1",
            CausationId: null,
            UserId: "user-1",
            Extensions: null);

    // --- Command dispatch tests ---

    [Fact]
    public async Task ProcessAsync_MatchingHandleMethod_DispatchesCorrectly() {
        var aggregate = new TestAggregate();
        CommandEnvelope command = CreateCommand(new AddItem("widget"));

        DomainResult result = await aggregate.ProcessAsync(command, null);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Events);
        Assert.IsType<ItemAdded>(result.Events[0]);
    }

    [Fact]
    public async Task ProcessAsync_UnknownCommandType_ThrowsInvalidOperationException() {
        var aggregate = new TestAggregate();
        CommandEnvelope command = CreateCommand(new UnknownCommand());

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => aggregate.ProcessAsync(command, null));

        Assert.Contains("UnknownCommand", ex.Message);
        Assert.Contains("TestAggregate", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_EmptyPayload_ThrowsInvalidOperationException() {
        var aggregate = new TestAggregate();
        CommandEnvelope command = CreateEmptyPayloadCommand("AddItem");

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => aggregate.ProcessAsync(command, null));
    }

    // --- State rehydration tests ---

    [Fact]
    public async Task ProcessAsync_NullState_PassesNullToHandle() {
        var aggregate = new TestAggregate();
        CommandEnvelope command = CreateCommand(new ResetItems());

        DomainResult result = await aggregate.ProcessAsync(command, null);

        Assert.True(result.IsNoOp); // state is null, count is 0, so NoOp
    }

    [Fact]
    public async Task ProcessAsync_TypedState_UsesDirectly() {
        var aggregate = new TestAggregate();
        var state = new TestState();
        // Apply some events to set count > 0
        state.Apply(new ItemAdded { Name = "a" });
        state.Apply(new ItemAdded { Name = "b" });
        CommandEnvelope command = CreateCommand(new RemoveItem());

        DomainResult result = await aggregate.ProcessAsync(command, state);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ProcessAsync_JsonElementObject_DeserializesToState() {
        var aggregate = new TestAggregate();
        string json = """{"ItemCount":2,"LastAdded":"from-json"}""";
        JsonElement jsonState = JsonSerializer.Deserialize<JsonElement>(json);
        CommandEnvelope command = CreateCommand(new RemoveItem());

        DomainResult result = await aggregate.ProcessAsync(command, jsonState);

        Assert.True(result.IsSuccess);
        _ = Assert.IsType<ItemRemoved>(result.Events[0]);
    }

    [Fact]
    public async Task ProcessAsync_JsonElementNull_ReturnsNullState() {
        var aggregate = new TestAggregate();
        JsonElement jsonNull = JsonSerializer.Deserialize<JsonElement>("null");
        CommandEnvelope command = CreateCommand(new ResetItems());

        DomainResult result = await aggregate.ProcessAsync(command, jsonNull);

        Assert.True(result.IsNoOp); // null state → count 0 → NoOp
    }

    [Fact]
    public async Task ProcessAsync_JsonElementArray_ReplaysEvents() {
        var aggregate = new TestAggregate();
        string eventsJson = """
            [
                {"eventTypeName":"ItemAdded","payload":{"Name":"first"}},
                {"eventTypeName":"ItemAdded","payload":{"Name":"second"}}
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(eventsJson);
        CommandEnvelope command = CreateCommand(new RemoveItem());

        DomainResult result = await aggregate.ProcessAsync(command, jsonArray);

        Assert.True(result.IsSuccess); // 2 items added, remove should succeed
    }

    [Fact]
    public async Task ProcessAsync_EnumerableEvents_ReplaysViaApply() {
        var aggregate = new TestAggregate();
        var events = new object[] {
            new ItemAdded { Name = "one" },
            new ItemAdded { Name = "two" },
        };
        CommandEnvelope command = CreateCommand(new RemoveItem());

        DomainResult result = await aggregate.ProcessAsync(command, events);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ProcessAsync_JsonElementArray_WithUnknownEventType_ThrowsInvalidOperationException() {
        var aggregate = new TestAggregate();
        string eventsJson = """
            [
                {"eventTypeName":"UnknownEvent","payload":{}}
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(eventsJson);
        CommandEnvelope command = CreateCommand(new ResetItems());

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => aggregate.ProcessAsync(command, jsonArray));
    }

    [Fact]
    public async Task ProcessAsync_EnumerableEvents_WithUnknownEventType_ThrowsInvalidOperationException() {
        var aggregate = new TestAggregate();
        var events = new object[] { new UnknownCommand() };
        CommandEnvelope command = CreateCommand(new ResetItems());

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => aggregate.ProcessAsync(command, events));
    }

    [Fact]
    public async Task ProcessAsync_StringState_ThrowsInvalidOperationException() {
        var aggregate = new TestAggregate();
        CommandEnvelope command = CreateCommand(new AddItem("test"));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => aggregate.ProcessAsync(command, "not-a-state"));
    }

    [Fact]
    public async Task ProcessAsync_WrongStateType_ThrowsInvalidOperationException() {
        var aggregate = new TestAggregate();
        CommandEnvelope command = CreateCommand(new AddItem("test"));

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => aggregate.ProcessAsync(command, 42)); // int is not TState

        Assert.Contains("TestState", ex.Message);
    }

    // --- Handle method return type tests ---

    [Fact]
    public async Task ProcessAsync_SyncHandleReturnsSuccess_ReturnsCorrectly() {
        var aggregate = new TestAggregate();
        CommandEnvelope command = CreateCommand(new AddItem("sync-test"));

        DomainResult result = await aggregate.ProcessAsync(command, null);

        Assert.True(result.IsSuccess);
        var evt = Assert.IsType<ItemAdded>(result.Events[0]);
        Assert.Equal("sync-test", evt.Name);
    }

    [Fact]
    public async Task ProcessAsync_SyncHandleReturnsRejection_ReturnsCorrectly() {
        var aggregate = new TestAggregate();
        CommandEnvelope command = CreateCommand(new RemoveItem());

        DomainResult result = await aggregate.ProcessAsync(command, null);

        Assert.True(result.IsRejection);
        Assert.IsType<ItemCannotBeRemoved>(result.Events[0]);
    }

    [Fact]
    public async Task ProcessAsync_SyncHandleReturnsNoOp_ReturnsCorrectly() {
        var aggregate = new TestAggregate();
        CommandEnvelope command = CreateCommand(new ResetItems());

        DomainResult result = await aggregate.ProcessAsync(command, null);

        Assert.True(result.IsNoOp);
    }

    [Fact]
    public async Task ProcessAsync_AsyncHandle_AwaitsAndReturnsCorrectly() {
        var aggregate = new AsyncTestAggregate();
        CommandEnvelope command = CreateCommand(new AsyncAddItem("async-test"));

        DomainResult result = await aggregate.ProcessAsync(command, null);

        Assert.True(result.IsSuccess);
        var evt = Assert.IsType<ItemAdded>(result.Events[0]);
        Assert.Equal("async-test", evt.Name);
    }

    [Fact]
    public async Task ProcessAsync_MixedSyncAsyncHandlers_BothWorkCorrectly() {
        var aggregate = new MixedAggregate();

        CommandEnvelope syncCommand = CreateCommand(new AddItem("sync"));
        DomainResult syncResult = await aggregate.ProcessAsync(syncCommand, null);
        Assert.True(syncResult.IsSuccess);

        CommandEnvelope asyncCommand = CreateCommand(new AsyncAddItem("async"));
        DomainResult asyncResult = await aggregate.ProcessAsync(asyncCommand, null);
        Assert.True(asyncResult.IsSuccess);
    }

    // --- Reflection cache tests ---

    [Fact]
    public async Task ProcessAsync_MultipleCalls_UsesCache() {
        var aggregate = new TestAggregate();

        // Call twice — second call should use cached metadata
        DomainResult result1 = await aggregate.ProcessAsync(CreateCommand(new AddItem("a")), null);
        DomainResult result2 = await aggregate.ProcessAsync(CreateCommand(new AddItem("b")), null);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
    }

    [Fact]
    public async Task ProcessAsync_DifferentAggregateTypes_IndependentCaches() {
        var testAggregate = new TestAggregate();
        var otherAggregate = new OtherAggregate();

        // Both have AddItem handlers but different state types
        DomainResult testResult = await testAggregate.ProcessAsync(CreateCommand(new AddItem("test")), null);
        DomainResult otherResult = await otherAggregate.ProcessAsync(CreateCommand(new AddItem("other")), null);

        Assert.True(testResult.IsSuccess);
        Assert.True(otherResult.IsSuccess);
    }

    // --- Null command guard ---

    [Fact]
    public async Task ProcessAsync_NullCommand_ThrowsArgumentNullException() {
        var aggregate = new TestAggregate();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => aggregate.ProcessAsync(null!, null));
    }
}
