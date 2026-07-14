using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Integration.Benchmarking;

using Shouldly;

namespace Hexalith.EventStore.Testing.Integration.Tests.Benchmarking;

public class BenchmarkDatasetBuilderTests {
    private static readonly DateTimeOffset _timestamp = new(2026, 7, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SeedAsync_WritesBoundedTransactionsWithMetadataLastThenCleanupRemovesState() {
        var handler = new RecordingActorStateHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var allocator = new RecordingGlobalPositionAllocator(501);
        var lifecycle = new RecordingBenchmarkActorLifecycle();
        var options = new BenchmarkDatasetBuilderOptions {
            MaxOperationsPerTransaction = 2,
            MaxTransactionBytes = 4096,
            MaxConcurrentActors = 1,
        };
        using var builder = new BenchmarkDatasetBuilder(
            httpClient,
            new Uri("http://localhost:3500"),
            allocator,
            "AggregateActor",
            lifecycle,
            options);
        AggregateIdentity identity = new("tenant-a", "counter", "benchmark-a");
        BenchmarkDatasetDefinition definition = CreateDefinition(identity, 3, includeSnapshot: true);

        BenchmarkDatasetReceipt receipt = await builder.SeedAsync(definition, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        receipt.DatasetId.ShouldBe("unit-dataset");
        receipt.Fingerprint.Length.ShouldBe(64);
        receipt.EventCount.ShouldBe(3);
        receipt.FirstGlobalPosition.ShouldBe(501);
        receipt.LastGlobalPosition.ShouldBe(503);
        allocator.AllocationCount.ShouldBe(1);
        allocator.RequestedCount.ShouldBe(3);
        lifecycle.ActivationCount.ShouldBe(1);
        lifecycle.DeactivationCount.ShouldBe(1);
        handler.TransactionKeys.Count.ShouldBe(3);
        handler.TransactionKeys[0].ShouldBe([
            $"{identity.EventStreamKeyPrefix}1",
            $"{identity.EventStreamKeyPrefix}2",
        ]);
        handler.TransactionKeys[1].ShouldBe([$"{identity.EventStreamKeyPrefix}3"]);
        handler.TransactionKeys[2].ShouldBe([identity.SnapshotKey, identity.MetadataKey]);
        handler.TransactionKeys.SelectMany(static keys => keys).Last().ShouldBe(identity.MetadataKey);
        handler.TransactionKeys.All(keys => keys.Count <= options.MaxOperationsPerTransaction).ShouldBeTrue();
        handler.TransactionBytes.All(bytes => bytes <= options.MaxTransactionBytes).ShouldBeTrue();
        handler.ContainsState(identity, identity.MetadataKey).ShouldBeTrue();
        handler.ContainsState(identity, identity.SnapshotKey).ShouldBeTrue();

        await builder.CleanupAsync(receipt, TestContext.Current.CancellationToken).ConfigureAwait(true);

        handler.ContainsState(identity, identity.MetadataKey).ShouldBeFalse();
        handler.ContainsState(identity, identity.SnapshotKey).ShouldBeFalse();
        for (int sequence = 1; sequence <= 3; sequence++) {
            handler.ContainsState(identity, $"{identity.EventStreamKeyPrefix}{sequence}").ShouldBeFalse();
        }

        allocator.AllocationCount.ShouldBe(1);
        lifecycle.ActivationCount.ShouldBe(2);
        lifecycle.DeactivationCount.ShouldBe(2);
    }

    [Fact]
    public async Task SeedAsync_InvalidEventFailsBeforePreflightOrRangeAllocation() {
        var handler = new RecordingActorStateHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var allocator = new RecordingGlobalPositionAllocator(1);
        var lifecycle = new RecordingBenchmarkActorLifecycle();
        using var builder = new BenchmarkDatasetBuilder(
            httpClient,
            new Uri("http://localhost:3500"),
            allocator,
            "AggregateActor",
            lifecycle);
        AggregateIdentity identity = new("tenant-a", "counter", "invalid-event");
        BenchmarkDatasetDefinition valid = CreateDefinition(identity, 1, includeSnapshot: false);
        BenchmarkEventDefinition source = valid.Aggregates[0].Events[0];
        var definition = valid with {
            Aggregates = [valid.Aggregates[0] with { Events = [source with { MessageId = " " }] }],
        };

        await Should.ThrowAsync<ArgumentException>(
            () => builder.SeedAsync(definition, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        handler.GetCount.ShouldBe(0);
        handler.PostCount.ShouldBe(0);
        allocator.AllocationCount.ShouldBe(0);
        lifecycle.ActivationCount.ShouldBe(0);
    }

    [Fact]
    public async Task SeedAsync_PreflightsEveryStreamBeforeAllocatingAndRejectsExistingMetadata() {
        var handler = new RecordingActorStateHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var allocator = new RecordingGlobalPositionAllocator(1);
        var lifecycle = new RecordingBenchmarkActorLifecycle();
        var options = new BenchmarkDatasetBuilderOptions { MaxConcurrentActors = 1 };
        using var builder = new BenchmarkDatasetBuilder(
            httpClient,
            new Uri("http://localhost:3500"),
            allocator,
            "AggregateActor",
            lifecycle,
            options);
        AggregateIdentity firstIdentity = new("tenant-a", "counter", "aggregate-a");
        AggregateIdentity existingIdentity = new("tenant-a", "counter", "aggregate-b");
        handler.SeedState(existingIdentity, existingIdentity.MetadataKey, new AggregateMetadata(1, _timestamp, null));
        var definition = new BenchmarkDatasetDefinition(
            "preflight-dataset",
            [
                CreateAggregate(firstIdentity, 1, includeSnapshot: false),
                CreateAggregate(existingIdentity, 1, includeSnapshot: false),
            ]);

        await Should.ThrowAsync<InvalidOperationException>(
            () => builder.SeedAsync(definition, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        handler.GetCount.ShouldBe(2);
        handler.PostCount.ShouldBe(0);
        allocator.AllocationCount.ShouldBe(0);
        lifecycle.ActivationCount.ShouldBe(0);
    }

    [Fact]
    public async Task SeedAsync_ProductionActorVisibilityCheckRejectsRacedStreamBeforeAllocation() {
        var handler = new RecordingActorStateHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var allocator = new RecordingGlobalPositionAllocator(1);
        var lifecycle = new RecordingBenchmarkActorLifecycle();
        using var builder = new BenchmarkDatasetBuilder(
            httpClient,
            new Uri("http://localhost:3500"),
            allocator,
            "AggregateActor",
            lifecycle);
        AggregateIdentity identity = new("tenant-a", "counter", "actor-visible-stream");
        lifecycle.SetMetadata(identity, new AggregateStreamMetadata(Exists: true, CurrentSequence: 1));
        BenchmarkDatasetDefinition definition = CreateDefinition(identity, 1, includeSnapshot: false);

        await Should.ThrowAsync<InvalidOperationException>(
            () => builder.SeedAsync(definition, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        handler.GetCount.ShouldBe(1);
        handler.PostCount.ShouldBe(0);
        allocator.AllocationCount.ShouldBe(0);
        lifecycle.ActivationCount.ShouldBe(1);
        lifecycle.DeactivationCount.ShouldBe(1);
    }

    [Fact]
    public async Task SeedAsync_AllocationFailureIsNotMaskedWhenStrictDeactivationAlsoFails() {
        var handler = new RecordingActorStateHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var allocationException = new ApplicationException("Synthetic allocation failure.");
        var allocator = new RecordingGlobalPositionAllocator(1) { AllocationException = allocationException };
        var lifecycle = new RecordingBenchmarkActorLifecycle {
            DeactivationException = new InvalidOperationException("Synthetic deactivation failure."),
        };
        using var builder = new BenchmarkDatasetBuilder(
            httpClient,
            new Uri("http://localhost:3500"),
            allocator,
            "AggregateActor",
            lifecycle);
        BenchmarkDatasetDefinition definition = CreateDefinition(
            new AggregateIdentity("tenant-a", "counter", "allocation-failure"),
            1,
            includeSnapshot: false);

        ApplicationException exception = await Should.ThrowAsync<ApplicationException>(
            () => builder.SeedAsync(definition, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        exception.ShouldBeSameAs(allocationException);
        exception.Data["BenchmarkActorDeactivationFailure"].ShouldBe(typeof(InvalidOperationException).FullName);
        handler.PostCount.ShouldBe(0);
        allocator.AllocationCount.ShouldBe(1);
        lifecycle.ActivationCount.ShouldBe(1);
        lifecycle.DeactivationCount.ShouldBe(1);
    }

    [Fact]
    public async Task SeedAsync_SnapshotFinalTransactionOperationBoundFailsBeforePreflightOrAllocation() {
        var handler = new RecordingActorStateHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var allocator = new RecordingGlobalPositionAllocator(1);
        var lifecycle = new RecordingBenchmarkActorLifecycle();
        var options = new BenchmarkDatasetBuilderOptions {
            MaxOperationsPerTransaction = 1,
            MaxTransactionBytes = 4096,
        };
        using var builder = new BenchmarkDatasetBuilder(
            httpClient,
            new Uri("http://localhost:3500"),
            allocator,
            "AggregateActor",
            lifecycle,
            options);
        BenchmarkDatasetDefinition definition = CreateDefinition(
            new AggregateIdentity("tenant-a", "counter", "operation-bound"),
            1,
            includeSnapshot: true);

        ArgumentException exception = await Should.ThrowAsync<ArgumentException>(
            () => builder.SeedAsync(definition, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        exception.Message.ShouldContain("visibility barrier");
        exception.Message.ShouldContain("2 operations");
        handler.GetCount.ShouldBe(0);
        handler.PostCount.ShouldBe(0);
        allocator.AllocationCount.ShouldBe(0);
        lifecycle.ActivationCount.ShouldBe(0);
    }

    [Fact]
    public async Task SeedAsync_SnapshotFinalTransactionByteBoundFailsBeforePreflightOrAllocation() {
        var handler = new RecordingActorStateHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var allocator = new RecordingGlobalPositionAllocator(1);
        var lifecycle = new RecordingBenchmarkActorLifecycle();
        var options = new BenchmarkDatasetBuilderOptions {
            MaxOperationsPerTransaction = 2,
            MaxTransactionBytes = 2000,
        };
        using var builder = new BenchmarkDatasetBuilder(
            httpClient,
            new Uri("http://localhost:3500"),
            allocator,
            "AggregateActor",
            lifecycle,
            options);
        AggregateIdentity identity = new("tenant-a", "counter", "byte-bound");
        BenchmarkAggregateDefinition aggregate = CreateAggregate(identity, 1, includeSnapshot: false) with {
            Snapshot = new BenchmarkSnapshotDefinition(
                1,
                JsonSerializer.SerializeToElement(new { Value = new string('x', 1500) }),
                _timestamp,
                EventStorePayloadProtectionMetadata.Unprotected()),
        };
        var definition = new BenchmarkDatasetDefinition("byte-bound-dataset", [aggregate]);

        ArgumentException exception = await Should.ThrowAsync<ArgumentException>(
            () => builder.SeedAsync(definition, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        exception.Message.ShouldContain("visibility barrier");
        exception.Message.ShouldContain("bytes");
        handler.GetCount.ShouldBe(0);
        handler.PostCount.ShouldBe(0);
        allocator.AllocationCount.ShouldBe(0);
        lifecycle.ActivationCount.ShouldBe(0);
    }

    [Fact]
    public async Task SeedAsync_OversizedEventFailsBeforePreflightOrRangeAllocation() {
        var handler = new RecordingActorStateHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var allocator = new RecordingGlobalPositionAllocator(1);
        var lifecycle = new RecordingBenchmarkActorLifecycle();
        var options = new BenchmarkDatasetBuilderOptions { MaxTransactionBytes = 256 };
        using var builder = new BenchmarkDatasetBuilder(
            httpClient,
            new Uri("http://localhost:3500"),
            allocator,
            "AggregateActor",
            lifecycle,
            options);
        AggregateIdentity identity = new("tenant-a", "counter", "oversized-event");
        BenchmarkDatasetDefinition source = CreateDefinition(identity, 1, includeSnapshot: false);
        BenchmarkEventDefinition sourceEvent = source.Aggregates[0].Events[0];
        var definition = source with {
            Aggregates = [source.Aggregates[0] with { Events = [sourceEvent with { Payload = new byte[4096] }] }],
        };

        await Should.ThrowAsync<ArgumentException>(
            () => builder.SeedAsync(definition, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        handler.GetCount.ShouldBe(0);
        handler.PostCount.ShouldBe(0);
        allocator.AllocationCount.ShouldBe(0);
        lifecycle.ActivationCount.ShouldBe(0);
    }

    private static BenchmarkAggregateDefinition CreateAggregate(
        AggregateIdentity identity,
        int eventCount,
        bool includeSnapshot) {
        BenchmarkEventDefinition[] events = [
            .. Enumerable.Range(1, eventCount).Select(sequence => new BenchmarkEventDefinition(
                $"message-{sequence}",
                _timestamp.AddSeconds(sequence),
                $"correlation-{sequence}",
                $"causation-{sequence}",
                "benchmark-user",
                "1.0.0",
                "CounterIncremented",
                1,
                "application/json",
                JsonSerializer.SerializeToUtf8Bytes(new { Amount = sequence }),
                EventStorePayloadProtectionMetadata.Unprotected())),
        ];
        BenchmarkSnapshotDefinition? snapshot = includeSnapshot
            ? new BenchmarkSnapshotDefinition(
                1,
                JsonSerializer.SerializeToElement(new { Count = 1 }),
                _timestamp,
                EventStorePayloadProtectionMetadata.Unprotected())
            : null;
        return new BenchmarkAggregateDefinition(identity, "Counter", events, snapshot);
    }

    private static BenchmarkDatasetDefinition CreateDefinition(
        AggregateIdentity identity,
        int eventCount,
        bool includeSnapshot) =>
        new("unit-dataset", [CreateAggregate(identity, eventCount, includeSnapshot)]);
}
