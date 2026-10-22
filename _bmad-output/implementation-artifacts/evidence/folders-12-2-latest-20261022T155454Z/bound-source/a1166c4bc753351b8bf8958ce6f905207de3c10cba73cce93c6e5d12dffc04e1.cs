
using System.Text.Json;

using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Tests.Handlers;

public class DomainProcessorTests : IDisposable {
    public DomainProcessorTests() {
        AssemblyScanner.ClearCache();
        NamingConventionEngine.ClearCache();
    }

    public void Dispose() {
        AssemblyScanner.ClearCache();
        NamingConventionEngine.ClearCache();
        GC.SuppressFinalize(this);
    }

    private sealed class TestState {
        public string Value { get; init; } = "default";

        public int AppliedCount { get; private set; }

        public void Apply(TestEventApplied _) => AppliedCount++;
    }

    private sealed class WrongState {
        public int Number { get; init; }
    }

    private sealed class TestEvent : IEventPayload;

    private sealed class TestEventApplied : IEventPayload;

    private sealed class StateCapturingProcessor : DomainProcessorBase<TestState> {
        public TestState? CapturedState { get; private set; }

        public bool HandleAsyncCalled { get; private set; }

        protected override Task<DomainResult> HandleAsync(CommandEnvelope command, TestState? currentState) {
            HandleAsyncCalled = true;
            CapturedState = currentState;
            var events = new IEventPayload[] { new TestEvent() };
            return Task.FromResult(DomainResult.Success(events));
        }
    }

    private sealed class DirectProcessor : IDomainProcessor {
        public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState) => Task.FromResult(DomainResult.NoOp());
    }

    private static CommandEnvelope CreateTestCommand() => new(
            MessageId: Guid.NewGuid().ToString(),
            TenantId: "tenant-1",
            Domain: "test-domain",
            AggregateId: "agg-1",
            CommandType: "TestCommand",
            Payload: [0x01],
            CorrelationId: "corr-1",
            CausationId: null,
            UserId: "user-1",
            Extensions: null);

    private static EventEnvelope CreateEnvelope<TEvent>(TEvent payload, long sequenceNumber = 1)
        where TEvent : IEventPayload {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        return new EventEnvelope(
            new EventMetadata(
                MessageId: Guid.NewGuid().ToString(),
                AggregateId: "agg-1",
                AggregateType: "test-aggregate",
                TenantId: "tenant-1",
                Domain: "test-domain",
                SequenceNumber: sequenceNumber,
                GlobalPosition: sequenceNumber,
                Timestamp: DateTimeOffset.UtcNow,
                CorrelationId: "corr-1",
                CausationId: "corr-1",
                UserId: "user-1",
                DomainServiceVersion: "v1",
                EventTypeName: typeof(TEvent).FullName ?? typeof(TEvent).Name,
                MetadataVersion: 1,
                SerializationFormat: "json"),
            bytes,
            null);
    }

    [Fact]
    public async Task DirectProcessor_ProcessAsync_ReturnsDomainResult() {
        IDomainProcessor processor = new DirectProcessor();
        CommandEnvelope command = CreateTestCommand();

        DomainResult result = await processor.ProcessAsync(command, null);

        Assert.True(result.IsNoOp);
    }

    [Fact]
    public async Task DomainProcessorBase_WithValidState_CastsCorrectly() {
        var processor = new StateCapturingProcessor();
        var state = new TestState { Value = "test-value" };
        CommandEnvelope command = CreateTestCommand();

        DomainResult result = await processor.ProcessAsync(command, state);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Events);
        Assert.Same(state, processor.CapturedState);
    }

    [Fact]
    public async Task DomainProcessorBase_WithNullState_PassesNullThrough() {
        var processor = new StateCapturingProcessor();
        CommandEnvelope command = CreateTestCommand();

        DomainResult result = await processor.ProcessAsync(command, null);

        Assert.True(result.IsSuccess);
        Assert.Null(processor.CapturedState);
        Assert.True(processor.HandleAsyncCalled);
    }

    [Fact]
    public async Task DomainProcessorBase_WithNullCommand_ThrowsArgumentNullException() {
        var processor = new StateCapturingProcessor();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => processor.ProcessAsync(null!, null));
    }

    [Fact]
    public async Task DomainProcessorBase_WithWrongStateType_ThrowsInvalidOperationException() {
        var processor = new StateCapturingProcessor();
        var wrongState = new WrongState { Number = 42 };
        CommandEnvelope command = CreateTestCommand();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => processor.ProcessAsync(command, wrongState));

        Assert.Contains("TestState", exception.Message);
        Assert.Contains("WrongState", exception.Message);
    }

    [Fact]
    public async Task DomainProcessorBase_WithJsonElement_DeserializesToTypedState() {
        var processor = new StateCapturingProcessor();
        var originalState = new TestState { Value = "from-json" };
        JsonElement jsonState = JsonSerializer.Deserialize<JsonElement>(
            JsonSerializer.Serialize(originalState));
        CommandEnvelope command = CreateTestCommand();

        DomainResult result = await processor.ProcessAsync(command, jsonState);

        Assert.True(result.IsSuccess);
        Assert.NotNull(processor.CapturedState);
        Assert.Equal("from-json", processor.CapturedState!.Value);
    }

    [Fact]
    public async Task DomainProcessorBase_WithNullJsonElement_DeserializesToNull() {
        var processor = new StateCapturingProcessor();
        JsonElement jsonState = JsonSerializer.Deserialize<JsonElement>("null");
        CommandEnvelope command = CreateTestCommand();

        // JsonElement with ValueKind.Null deserializes to null for reference types
        DomainResult result = await processor.ProcessAsync(command, jsonState);

        Assert.True(result.IsSuccess);
        Assert.Null(processor.CapturedState);
    }

    [Fact]
    public async Task DomainProcessorBase_WithEventEnvelopeEnumerable_RehydratesViaApply() {
        var processor = new StateCapturingProcessor();
        CommandEnvelope command = CreateTestCommand();
        object[] currentState = new object[] { CreateEnvelope(new TestEventApplied()), CreateEnvelope(new TestEventApplied(), 2) };

        DomainResult result = await processor.ProcessAsync(command, currentState);

        Assert.True(result.IsSuccess);
        Assert.NotNull(processor.CapturedState);
        Assert.Equal(2, processor.CapturedState!.AppliedCount);
    }

    [Fact]
    public async Task DomainProcessorBase_WithSnapshotAwareCurrentState_RehydratesSnapshotAndTail() {
        var processor = new StateCapturingProcessor();
        CommandEnvelope command = CreateTestCommand();
        var currentState = new DomainServiceCurrentState(
            new TestState { Value = "from-snapshot" },
            [CreateEnvelope(new TestEventApplied())],
            5,
            6);

        DomainResult result = await processor.ProcessAsync(command, currentState);

        Assert.True(result.IsSuccess);
        Assert.NotNull(processor.CapturedState);
        Assert.Equal("from-snapshot", processor.CapturedState!.Value);
        Assert.Equal(1, processor.CapturedState.AppliedCount);
    }
}
