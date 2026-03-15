
using System.Text.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

public class EventStoreAggregateTests : IDisposable {
    public EventStoreAggregateTests() {
        AssemblyScanner.ClearCache();
        NamingConventionEngine.ClearCache();
    }

    public void Dispose() {
        AssemblyScanner.ClearCache();
        NamingConventionEngine.ClearCache();
        GC.SuppressFinalize(this);
    }

    // --- Test Event Types ---
    private sealed class ItemAdded : IEventPayload {
        public string Name { get; init; } = string.Empty;
    }

    private sealed class ItemRemoved : IEventPayload;

    private sealed class ItemReset : IEventPayload;

    private sealed class ItemCannotBeRemoved : IRejectionEvent;

    // --- Record event types (matching production patterns like CounterIncremented) ---
    private sealed record CounterIncremented : IEventPayload;

    private sealed record CounterDecremented : IEventPayload;

    private sealed record CounterReset : IEventPayload;

    private sealed record CounterCannotGoNegative : IRejectionEvent;

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

    // --- Counter state for record-based aggregate ---
    private sealed class CounterState {
        public int Count { get; private set; }

        public void Apply(CounterIncremented e) => Count++;

        public void Apply(CounterDecremented e) => Count--;

        public void Apply(CounterReset e) => Count = 0;
    }

    // --- Test Commands ---
    private sealed record AddItem(string Name);

    private sealed record RemoveItem;

    private sealed record IncrementCounter;

    private sealed record DecrementCounter;

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

    // --- Record-based aggregate (mirrors production CounterAggregate pattern) ---
    private sealed class RecordAggregate : EventStoreAggregate<CounterState> {
        public static DomainResult Handle(IncrementCounter command, CounterState? state)
            => DomainResult.Success(new IEventPayload[] { new CounterIncremented() });

        public static DomainResult Handle(DecrementCounter command, CounterState? state) {
            if ((state?.Count ?? 0) == 0) {
                return DomainResult.Rejection(new IRejectionEvent[] { new CounterCannotGoNegative() });
            }

            return DomainResult.Success(new IEventPayload[] { new CounterDecremented() });
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

    // --- Test Aggregate with INSTANCE (non-static) Handle methods ---
    private sealed class InstanceHandleAggregate : EventStoreAggregate<TestState> {
        private readonly string _prefix = "instance";

        public DomainResult Handle(AddItem command, TestState? state)
            => DomainResult.Success(new IEventPayload[] { new ItemAdded { Name = $"{_prefix}-{command.Name}" } });
    }

    // --- Test Aggregate with wrong return type Handle method (should be silently skipped) ---
    private sealed class WrongReturnTypeCommand;

    private sealed class WrongReturnTypeAggregate : EventStoreAggregate<TestState> {
        // This Handle method returns string instead of DomainResult — discovery should skip it
        public static string Handle(WrongReturnTypeCommand command, TestState? state) => "not-a-domain-result";

        // Valid handler for AddItem so the aggregate isn't completely empty
        public static DomainResult Handle(AddItem command, TestState? state)
            => DomainResult.Success(new IEventPayload[] { new ItemAdded { Name = command.Name } });
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
            MessageId: Guid.NewGuid().ToString(),
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
            MessageId: Guid.NewGuid().ToString(),
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
        _ = Assert.IsType<ItemAdded>(result.Events[0]);
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
        object[] events = new object[] {
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
    public async Task ProcessAsync_JsonElementArray_WithNonObjectEntry_ThrowsInvalidOperationException() {
        var aggregate = new TestAggregate();
        string eventsJson = """
            [
                42
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(eventsJson);
        CommandEnvelope command = CreateCommand(new ResetItems());

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => aggregate.ProcessAsync(command, jsonArray));
    }

    [Fact]
    public async Task ProcessAsync_JsonElementArray_WithMissingEventTypeName_ThrowsInvalidOperationException() {
        var aggregate = new TestAggregate();
        string eventsJson = """
            [
                {"payload":{"Name":"x"}}
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(eventsJson);
        CommandEnvelope command = CreateCommand(new ResetItems());

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => aggregate.ProcessAsync(command, jsonArray));
    }

    [Fact]
    public async Task ProcessAsync_JsonElementArray_WithInvalidPayloadShape_ThrowsInvalidOperationException() {
        var aggregate = new TestAggregate();
        string eventsJson = """
            [
                {"eventTypeName":"ItemAdded","payload":123}
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
        object[] events = new object[] { new UnknownCommand() };
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
        ItemAdded evt = Assert.IsType<ItemAdded>(result.Events[0]);
        Assert.Equal("sync-test", evt.Name);
    }

    [Fact]
    public async Task ProcessAsync_SyncHandleReturnsRejection_ReturnsCorrectly() {
        var aggregate = new TestAggregate();
        CommandEnvelope command = CreateCommand(new RemoveItem());

        DomainResult result = await aggregate.ProcessAsync(command, null);

        Assert.True(result.IsRejection);
        _ = Assert.IsType<ItemCannotBeRemoved>(result.Events[0]);
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
        ItemAdded evt = Assert.IsType<ItemAdded>(result.Events[0]);
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

    [Fact]
    public async Task ProcessAsync_DifferentAggregateTypes_ConcurrentFirstUse_DoesNotInterfere() {
        const int iterations = 32;
        Task<DomainResult>[] calls = Enumerable.Range(0, iterations)
            .SelectMany(i => {
                var testAggregate = new TestAggregate();
                var otherAggregate = new OtherAggregate();

                return new[] {
                    testAggregate.ProcessAsync(CreateCommand(new AddItem($"t-{i}")), null),
                    otherAggregate.ProcessAsync(CreateCommand(new AddItem($"o-{i}")), null),
                };
            })
            .ToArray();

        DomainResult[] results = await Task.WhenAll(calls);

        Assert.Equal(iterations * 2, results.Length);
        Assert.All(results, r => Assert.True(r.IsSuccess));
    }

    // --- Null command guard ---

    [Fact]
    public async Task ProcessAsync_NullCommand_ThrowsArgumentNullException() {
        var aggregate = new TestAggregate();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => aggregate.ProcessAsync(null!, null));
    }

    // --- Story 16-8: Instance (non-static) Handle method (AC#5: 6.2) ---

    [Fact]
    public async Task ProcessAsync_InstanceHandleMethod_DispatchesCorrectly() {
        var aggregate = new InstanceHandleAggregate();
        CommandEnvelope command = CreateCommand(new AddItem("test"));

        DomainResult result = await aggregate.ProcessAsync(command, null);

        Assert.True(result.IsSuccess);
        ItemAdded evt = Assert.IsType<ItemAdded>(result.Events[0]);
        // Verifies instance data (_prefix) is accessible — proving non-static dispatch
        Assert.Equal("instance-test", evt.Name);
    }

    // --- Story 16-8: Multiple Handle methods dispatched correctly (AC#5: 6.3) ---

    [Fact]
    public async Task ProcessAsync_MultipleHandleMethods_AllDispatchCorrectly() {
        var aggregate = new TestAggregate();

        // Dispatch AddItem
        DomainResult addResult = await aggregate.ProcessAsync(CreateCommand(new AddItem("a")), null);
        Assert.True(addResult.IsSuccess);
        _ = Assert.IsType<ItemAdded>(addResult.Events[0]);

        // Dispatch RemoveItem (with state having items)
        var state = new TestState();
        state.Apply(new ItemAdded { Name = "a" });
        DomainResult removeResult = await aggregate.ProcessAsync(CreateCommand(new RemoveItem()), state);
        Assert.True(removeResult.IsSuccess);
        _ = Assert.IsType<ItemRemoved>(removeResult.Events[0]);

        // Dispatch ResetItems (with state having items)
        DomainResult resetResult = await aggregate.ProcessAsync(CreateCommand(new ResetItems()), state);
        Assert.True(resetResult.IsSuccess);
        _ = Assert.IsType<ItemReset>(resetResult.Events[0]);
    }

    // --- Story 16-8: JsonElement array suffix-match fallback (AC#5: 6.4) ---

    [Fact]
    public async Task ProcessAsync_JsonElementArray_SuffixMatchedEventTypeName_ReplaysCorrectly() {
        var aggregate = new TestAggregate();
        // Use fully-qualified-style eventTypeName that ends with "ItemAdded"
        string eventsJson = """
            [
                {"eventTypeName":"MyNamespace.ItemAdded","payload":{"Name":"suffix-match"}}
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(eventsJson);
        CommandEnvelope command = CreateCommand(new RemoveItem());

        // The suffix fallback should match "MyNamespace.ItemAdded" to the "ItemAdded" Apply method
        DomainResult result = await aggregate.ProcessAsync(command, jsonArray);

        Assert.True(result.IsSuccess);
    }

    // --- Story 16-8: IEnumerable replay with null elements (AC#5) ---

    [Fact]
    public async Task ProcessAsync_EnumerableEvents_WithNullElements_SkipsNulls() {
        var aggregate = new TestAggregate();
        object?[] events = new object?[] {
            new ItemAdded { Name = "one" },
            null,
            new ItemAdded { Name = "two" },
        };
        CommandEnvelope command = CreateCommand(new RemoveItem());

        DomainResult result = await aggregate.ProcessAsync(command, events);

        Assert.True(result.IsSuccess); // 2 items added, remove succeeds
    }

    // --- Base64 payload deserialization (EventEnvelope byte[] arrives as Base64 string) ---

    [Fact]
    public async Task ProcessAsync_JsonElementArray_Base64Payload_DeserializesCorrectly() {
        var aggregate = new TestAggregate();
        // Simulate EventEnvelope.Payload (byte[]) serialized as Base64 by System.Text.Json.
        // Base64 of '{"Name":"base64-test"}' is 'eyJOYW1lIjoiYmFzZTY0LXRlc3QifQ=='
        string payloadJson = """{"Name":"base64-test"}""";
        string base64Payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payloadJson));
        string eventsJson = $$"""
            [
                {"eventTypeName":"ItemAdded","payload":"{{base64Payload}}"}
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(eventsJson);
        CommandEnvelope command = CreateCommand(new RemoveItem());

        DomainResult result = await aggregate.ProcessAsync(command, jsonArray);

        Assert.True(result.IsSuccess); // 1 item added via Base64 payload, remove succeeds
    }

    [Fact]
    public async Task ProcessAsync_JsonElementArray_Base64EmptyRecordPayload_DeserializesCorrectly() {
        var aggregate = new TestAggregate();
        // Simulate empty record (like CounterIncremented) serialized as Base64.
        // Base64 of '{}' is 'e30='
        string base64Empty = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{}"));
        // Build JSON manually to avoid raw string interpolation brace conflicts
        string eventsJson = "[{\"eventTypeName\":\"ItemAdded\",\"payload\":{\"Name\":\"setup\"}},"
            + "{\"eventTypeName\":\"ItemRemoved\",\"payload\":\"" + base64Empty + "\"}]";
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(eventsJson);
        CommandEnvelope command = CreateCommand(new AddItem("after-base64"));

        DomainResult result = await aggregate.ProcessAsync(command, jsonArray);

        Assert.True(result.IsSuccess); // State rehydrated: 1 added - 1 removed = 0 items, then add succeeds
    }

    // --- Record event types with Base64 payload (Dapr wire format) ---
    // These tests use sealed record types (not classes) to match production event types
    // like CounterIncremented, and simulate the full Dapr EventEnvelope serialization format.

    [Fact]
    public async Task ProcessAsync_RecordEvent_Base64EmptyPayload_DeserializesCorrectly() {
        var aggregate = new RecordAggregate();
        // sealed record CounterIncremented serialized as Base64: {} → e30=
        string base64Empty = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{}"));
        string eventsJson = "[{\"eventTypeName\":\"CounterIncremented\",\"payload\":\"" + base64Empty + "\"}]";
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(eventsJson);
        CommandEnvelope command = CreateCommand(new DecrementCounter());

        DomainResult result = await aggregate.ProcessAsync(command, jsonArray);

        // State: 1 increment → count=1, then decrement succeeds
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ProcessAsync_DaprEventEnvelopeFormat_Base64Payload_DeserializesCorrectly() {
        var aggregate = new RecordAggregate();
        // Simulate the full Dapr EventEnvelope JSON format as it arrives via DaprClient.InvokeMethodAsync.
        // EventEnvelope has 12 fields; byte[] Payload is serialized as Base64 by System.Text.Json.
        string base64Empty = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{}"));
        string eventsJson = $$"""
            [
                {
                    "aggregateId": "counter-1",
                    "tenantId": "tenant-a",
                    "domain": "counter",
                    "sequenceNumber": 1,
                    "timestamp": "2026-03-15T09:00:00+00:00",
                    "correlationId": "corr-1",
                    "causationId": "corr-1",
                    "userId": "user-1",
                    "domainServiceVersion": "v1",
                    "eventTypeName": "CounterIncremented",
                    "serializationFormat": "json",
                    "payload": "{{base64Empty}}",
                    "extensions": null
                }
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(eventsJson);
        CommandEnvelope command = CreateCommand(new DecrementCounter());

        DomainResult result = await aggregate.ProcessAsync(command, jsonArray);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ProcessAsync_DaprEventEnvelopeFormat_MultipleEvents_RehydratesState() {
        var aggregate = new RecordAggregate();
        string base64Empty = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{}"));
        // Two increments then a decrement — final count should be 1
        string eventsJson = $$"""
            [
                {"eventTypeName":"CounterIncremented","payload":"{{base64Empty}}"},
                {"eventTypeName":"CounterIncremented","payload":"{{base64Empty}}"},
                {"eventTypeName":"CounterDecremented","payload":"{{base64Empty}}"}
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(eventsJson);
        // Decrement when count=1 should succeed
        CommandEnvelope command = CreateCommand(new DecrementCounter());

        DomainResult result = await aggregate.ProcessAsync(command, jsonArray);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ProcessAsync_DaprEventEnvelopeFormat_ResetEvent_RehydratesState() {
        var aggregate = new RecordAggregate();
        string base64Empty = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{}"));
        // Increment, then reset — final count should be 0
        string eventsJson = $$"""
            [
                {"eventTypeName":"CounterIncremented","payload":"{{base64Empty}}"},
                {"eventTypeName":"CounterReset","payload":"{{base64Empty}}"}
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(eventsJson);
        // Decrement when count=0 should be rejected
        CommandEnvelope command = CreateCommand(new DecrementCounter());

        DomainResult result = await aggregate.ProcessAsync(command, jsonArray);

        Assert.True(result.IsRejection);
    }

    [Fact]
    public async Task ProcessAsync_DaprSerializationRoundTrip_Base64Payload_Survives() {
        // Simulate the exact Dapr serialization round-trip:
        // 1. DaprClient.InvokeMethodAsync serializes DomainServiceRequest with JsonSerializerDefaults.Web
        // 2. Target service ASP.NET Core deserializes with Web defaults
        // 3. CurrentState (object?) becomes JsonElement
        // This catches bugs where byte[] → Base64 → JsonElement deserialization breaks.
        var aggregate = new RecordAggregate();
        byte[] emptyRecordPayload = JsonSerializer.SerializeToUtf8Bytes(new CounterIncremented());

        // Build the wire format as Dapr would: EventEnvelope with byte[] Payload
        var wireEvents = new[] {
            new {
                aggregateId = "counter-1",
                tenantId = "tenant-a",
                domain = "counter",
                sequenceNumber = 1L,
                timestamp = DateTimeOffset.UtcNow,
                correlationId = "corr-1",
                causationId = "corr-1",
                userId = "user-1",
                domainServiceVersion = "v1",
                eventTypeName = "CounterIncremented",
                serializationFormat = "json",
                payload = emptyRecordPayload, // byte[] — System.Text.Json serializes as Base64
                extensions = (Dictionary<string, string>?)null,
            },
        };

        // Step 1: Serialize with Web defaults (as DaprClient does)
        var webOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(wireEvents, webOptions);

        // Step 2: Deserialize as object? (as ASP.NET Core does for DomainServiceRequest.CurrentState)
        object? currentState = JsonSerializer.Deserialize<JsonElement>(serialized, webOptions);

        // Step 3: Process — EventStoreAggregate must handle the Base64 payload
        CommandEnvelope command = CreateCommand(new DecrementCounter());
        DomainResult result = await aggregate.ProcessAsync(command, currentState);

        Assert.True(result.IsSuccess); // 1 increment → count=1, decrement succeeds
    }

    // --- Story 1.4: Handle method with wrong return type silently skipped (AC#2: 2.4) ---

    [Fact]
    public async Task ProcessAsync_HandleMethodWithWrongReturnType_IsSilentlySkipped() {
        var aggregate = new WrongReturnTypeAggregate();
        // WrongReturnTypeCommand has a Handle method returning string — should be skipped
        CommandEnvelope command = CreateCommand(new WrongReturnTypeCommand());

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => aggregate.ProcessAsync(command, null));

        // The string-returning Handle was skipped, so no handler found for this command type
        Assert.Contains("WrongReturnTypeCommand", ex.Message);
        Assert.Contains("No Handle method found", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WrongReturnTypeAggregate_ValidHandler_StillWorks() {
        var aggregate = new WrongReturnTypeAggregate();
        // AddItem has a valid DomainResult-returning handler — should still work
        CommandEnvelope command = CreateCommand(new AddItem("valid"));

        DomainResult result = await aggregate.ProcessAsync(command, null);

        Assert.True(result.IsSuccess);
    }

    // --- Story 16-8: JsonElement array without payload wrapper (direct element) ---

    [Fact]
    public async Task ProcessAsync_JsonElementArray_DirectEvent_WithoutPayloadWrapper() {
        var aggregate = new TestAggregate();
        // Event element has no "payload" property — entire element is deserialized directly
        string eventsJson = """
            [
                {"eventTypeName":"ItemAdded","Name":"direct-event"}
            ]
            """;
        JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(eventsJson);
        CommandEnvelope command = CreateCommand(new RemoveItem());

        DomainResult result = await aggregate.ProcessAsync(command, jsonArray);

        Assert.True(result.IsSuccess);
    }
}
