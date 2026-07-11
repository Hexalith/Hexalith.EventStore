using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public class ProjectionStateEraserTests {
    [Fact]
    public async Task TryEraseAsync_AllTargetsShareStore_UsesOneAtomicTransaction() {
        var daprClient = new RecordingDaprClient();
        IOptions<ProjectionOptions> options = Options.Create(new ProjectionOptions { CheckpointStateStoreName = "statestore" });
        var tracker = new ProjectionCheckpointTracker(
            daprClient,
            options,
            NullLogger<ProjectionCheckpointTracker>.Instance);
        var eraser = new ProjectionStateEraser(
            daprClient,
            new DaprReadModelStore(daprClient),
            tracker,
            options);
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");

        bool erased = await eraser.TryEraseAsync(
            identity,
            [
                new ReadModelEraseTarget("tenant-a", "statestore", "tenant-a:orders:order-1:summary", "etag-summary"),
                new ReadModelEraseTarget("tenant-a", "statestore", "tenant-a:orders:order-1:audit", "etag-audit"),
            ],
            "etag-checkpoint");

        erased.ShouldBeTrue();
        daprClient.ExecuteStateTransactionCallCount.ShouldBe(1);
        daprClient.StoreName.ShouldBe("statestore");
        IReadOnlyList<StateTransactionRequest> operations = daprClient.TransactionOperations.ShouldNotBeNull();
        operations.Count.ShouldBe(3);
        operations.All(operation => operation.OperationType == StateOperationType.Delete).ShouldBeTrue();
        operations.All(operation => operation.Value != null && operation.Value.Length == 0).ShouldBeTrue();
        operations.All(operation => operation.Options != null && operation.Options.Concurrency == ConcurrencyMode.FirstWrite).ShouldBeTrue();
        operations.ShouldContain(operation => operation.Key == "tenant-a:orders:order-1:summary" && operation.ETag == "etag-summary");
        operations.ShouldContain(operation => operation.Key == "tenant-a:orders:order-1:audit" && operation.ETag == "etag-audit");
        operations.ShouldContain(operation => operation.Key == "projection-checkpoints:tenant-a:orders:order-1" && operation.ETag == "etag-checkpoint");
    }

    [Fact]
    public async Task TryEraseAsync_CrossStorePartialFailure_RetryConvergesToFullyErasedState() {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var readModelStore = new Hexalith.EventStore.Testing.Fakes.InMemoryReadModelStore();
        await readModelStore.SaveAsync("read-model-store", "tenant-a:orders:order-1:summary", new DaprReadModelStoreTestModel { Value = 1 });
        await readModelStore.SaveAsync("read-model-store", "tenant-a:orders:order-1:audit", new DaprReadModelStoreTestModel { Value = 2 });
        ReadModelEntry<DaprReadModelStoreTestModel> summary = await readModelStore.GetAsync<DaprReadModelStoreTestModel>("read-model-store", "tenant-a:orders:order-1:summary");
        ReadModelEntry<DaprReadModelStoreTestModel> audit = await readModelStore.GetAsync<DaprReadModelStoreTestModel>("read-model-store", "tenant-a:orders:order-1:audit");

        ProjectionCheckpoint? persistedCheckpoint = new(
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            7,
            DateTimeOffset.UtcNow);
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.TryDeleteStateAsync(
                "checkpoint-store",
                "projection-checkpoints:tenant-a:orders:order-1",
                "checkpoint-etag",
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => {
                persistedCheckpoint = null;
                return true;
            });
        IOptions<ProjectionOptions> options = Options.Create(new ProjectionOptions { CheckpointStateStoreName = "checkpoint-store" });
        var tracker = new ProjectionCheckpointTracker(daprClient, options, NullLogger<ProjectionCheckpointTracker>.Instance);
        var eraser = new ProjectionStateEraser(daprClient, readModelStore, tracker, options);
        ReadModelEraseTarget[] targets = [
            new("tenant-a", "read-model-store", "tenant-a:orders:order-1:summary", summary.ETag!),
            new("tenant-a", "read-model-store", "tenant-a:orders:order-1:audit", audit.ETag!),
        ];
        int eraseAttempts = 0;
        readModelStore.ConcurrentWriteBeforeTryErase = () => {
            if (++eraseAttempts == 2) {
                readModelStore.ConcurrentWriteBeforeTryErase = null;
                throw new InvalidOperationException("injected failure after first target");
            }
        };

        bool firstResult = await eraser.TryEraseAsync(identity, targets, "checkpoint-etag");

        firstResult.ShouldBeFalse();
        readModelStore.Snapshot<DaprReadModelStoreTestModel>("read-model-store", "tenant-a:orders:order-1:summary").ShouldBeNull();
        readModelStore.Snapshot<DaprReadModelStoreTestModel>("read-model-store", "tenant-a:orders:order-1:audit").ShouldNotBeNull();
        persistedCheckpoint.ShouldNotBeNull();

        bool retryResult = await eraser.TryEraseAsync(identity, targets, "checkpoint-etag");

        retryResult.ShouldBeTrue();
        readModelStore.Snapshot<DaprReadModelStoreTestModel>("read-model-store", "tenant-a:orders:order-1:summary").ShouldBeNull();
        readModelStore.Snapshot<DaprReadModelStoreTestModel>("read-model-store", "tenant-a:orders:order-1:audit").ShouldBeNull();
        persistedCheckpoint.ShouldBeNull();
    }
}
