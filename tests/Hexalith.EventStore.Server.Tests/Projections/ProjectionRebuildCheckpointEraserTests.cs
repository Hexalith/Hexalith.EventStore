using System.Reflection;

using Dapr;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public class ProjectionRebuildCheckpointEraserTests {
    private static readonly ProjectionRebuildCheckpointScope AggregateScope = new(
        "tenant-a",
        "party",
        "party-summary",
        "party-1",
        "01HX0000000000000000000000");

    private static readonly ProjectionRebuildCheckpointScope OperatorScope = new(
        "tenant-a",
        "party",
        "party-summary",
        null,
        "01HX0000000000000000000000");

    private const string AggregateKey = "projection-rebuild-checkpoints:tenant-a:party:party-summary:party-1";
    private const string OperatorKey = "projection-rebuild-checkpoints:tenant-a:party:party-summary:*";
    private const string ActiveIndexKey = "projection-rebuild-active-index:tenant-a:party";
    private const string ActivePairIndexKey = "projection-rebuild-active-index-pairs";

    [Fact]
    public async Task TryEraseAggregateCheckpointAsyncPresentRowWithCorrectEtagDeletesRow() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupDelete(daprClient, "etag-1", result: true);
        IProjectionRebuildCheckpointEraser eraser = CreateStore(daprClient);

        bool erased = await eraser.TryEraseAggregateCheckpointAsync(AggregateScope, "etag-1");

        erased.ShouldBeTrue();
        _ = await daprClient.Received(1).TryDeleteStateAsync(
            "statestore",
            AggregateKey,
            "etag-1",
            Arg.Is<StateOptions?>(value => value != null && value.Concurrency == ConcurrencyMode.FirstWrite),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryEraseAggregateCheckpointAsyncAbsentRowReturnsTrueIdempotently() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        // Absent row: the coordinator read (false, "") and passes the empty ETag. A FirstWrite delete
        // of a missing key is an idempotent success.
        SetupDelete(daprClient, string.Empty, result: true);
        IProjectionRebuildCheckpointEraser eraser = CreateStore(daprClient);

        bool erased = await eraser.TryEraseAggregateCheckpointAsync(AggregateScope, string.Empty);

        erased.ShouldBeTrue();
    }

    [Fact]
    public async Task TryEraseAggregateCheckpointAsyncStaleEtagReturnsFalseAndDoesNotDeleteRow() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        // FirstWrite conditional delete with a mismatched ETag fails; DAPR returns false and the row
        // is preserved.
        SetupDelete(daprClient, "stale-etag", result: false);
        IProjectionRebuildCheckpointEraser eraser = CreateStore(daprClient);

        bool erased = await eraser.TryEraseAggregateCheckpointAsync(AggregateScope, "stale-etag");

        erased.ShouldBeFalse();
        _ = await daprClient.Received(1).TryDeleteStateAsync(
            "statestore",
            AggregateKey,
            "stale-etag",
            Arg.Is<StateOptions?>(value => value != null && value.Concurrency == ConcurrencyMode.FirstWrite),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryEraseAggregateCheckpointAsyncEraseTargetsAggregateKeyNeverOperatorOrIndexKeys() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupDelete(daprClient, "etag-1", result: true);
        IProjectionRebuildCheckpointEraser eraser = CreateStore(daprClient);

        _ = await eraser.TryEraseAggregateCheckpointAsync(AggregateScope, "etag-1");

        _ = await daprClient.Received(1).TryDeleteStateAsync(
            "statestore",
            AggregateKey,
            "etag-1",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
        _ = await daprClient.DidNotReceive().TryDeleteStateAsync(
            Arg.Any<string>(),
            OperatorKey,
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
        _ = await daprClient.DidNotReceive().TryDeleteStateAsync(
            Arg.Any<string>(),
            ActiveIndexKey,
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
        _ = await daprClient.DidNotReceive().TryDeleteStateAsync(
            Arg.Any<string>(),
            ActivePairIndexKey,
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryReadAggregateCheckpointEtagAsyncPresentRowReturnsPresentTrueWithEtag() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        ProjectionRebuildCheckpoint existing = CreateCheckpoint(10, ProjectionRebuildStatus.Running);
        SetupReadEtag(daprClient, existing, "etag-9");
        IProjectionRebuildCheckpointEraser eraser = CreateStore(daprClient);

        (bool present, string etag) = await eraser.TryReadAggregateCheckpointEtagAsync(AggregateScope);

        present.ShouldBeTrue();
        etag.ShouldBe("etag-9");
        _ = await daprClient.Received(1).GetStateAndETagAsync<ProjectionRebuildCheckpoint>(
            "statestore",
            AggregateKey,
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryReadAggregateCheckpointEtagAsyncAbsentRowReturnsPresentFalseWithEmptyEtag() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupReadEtag(daprClient, null, string.Empty);
        IProjectionRebuildCheckpointEraser eraser = CreateStore(daprClient);

        (bool present, string etag) = await eraser.TryReadAggregateCheckpointEtagAsync(AggregateScope);

        present.ShouldBeFalse();
        etag.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task TryEraseAggregateCheckpointAsyncOperatorScopeThrowsAndPerformsNoStateAccess() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IProjectionRebuildCheckpointEraser eraser = CreateStore(daprClient);

        _ = await Should.ThrowAsync<ArgumentException>(
            () => eraser.TryEraseAggregateCheckpointAsync(OperatorScope, "etag-1"));

        _ = await daprClient.DidNotReceiveWithAnyArgs().TryDeleteStateAsync(
            default!,
            default!,
            default!,
            stateOptions: default,
            metadata: default,
            cancellationToken: default);
        _ = await daprClient.DidNotReceiveWithAnyArgs().GetStateAndETagAsync<ProjectionRebuildCheckpoint>(
            default!,
            default!,
            consistencyMode: default,
            metadata: default,
            cancellationToken: default);
    }

    [Fact]
    public async Task TryReadAggregateCheckpointEtagAsyncOperatorScopeThrowsAndPerformsNoStateAccess() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IProjectionRebuildCheckpointEraser eraser = CreateStore(daprClient);

        _ = await Should.ThrowAsync<ArgumentException>(
            () => eraser.TryReadAggregateCheckpointEtagAsync(OperatorScope));

        _ = await daprClient.DidNotReceiveWithAnyArgs().GetStateAndETagAsync<ProjectionRebuildCheckpoint>(
            default!,
            default!,
            consistencyMode: default,
            metadata: default,
            cancellationToken: default);
        _ = await daprClient.DidNotReceiveWithAnyArgs().TryDeleteStateAsync(
            default!,
            default!,
            default!,
            stateOptions: default,
            metadata: default,
            cancellationToken: default);
    }

    [Fact]
    public void ReleasedRebuildCheckpointStoreInterfaceShapeIsUnchanged() {
        string[] members = typeof(IProjectionRebuildCheckpointStore)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        members.ShouldBe(
            [
                "ClearOrphanActiveRebuildIndexEntriesAsync",
                "HasActiveOperatorRebuildForDomainAsync",
                "ListActiveRebuildIndexPairsAsync",
                "ReadAsync",
                "ResetAsync",
                "SaveAsync",
            ],
            ignoreOrder: false);

        // The erase capability must never leak onto the released rebuild-checkpoint contract.
        typeof(IProjectionRebuildCheckpointStore).GetMethod("TryEraseAggregateCheckpointAsync").ShouldBeNull();
        typeof(IProjectionRebuildCheckpointStore).GetMethod("TryReadAggregateCheckpointEtagAsync").ShouldBeNull();
    }

    [Fact]
    public void ActiveRebuildGateAlreadyPresentOnReleasedInterfaceAndNotAddedByEraser() {
        // Task 4 does NOT add a new active-rebuild gate: the coordinator's gate
        // (HasActiveOperatorRebuildForDomainAsync) already exists on the released interface, and the
        // Task 4 eraser interface intentionally exposes only the two erase/read members.
        typeof(IProjectionRebuildCheckpointStore)
            .GetMethod("HasActiveOperatorRebuildForDomainAsync")
            .ShouldNotBeNull();

        string[] eraserMembers = typeof(IProjectionRebuildCheckpointEraser)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        eraserMembers.ShouldBe(
            ["TryEraseAggregateCheckpointAsync", "TryReadAggregateCheckpointEtagAsync"],
            ignoreOrder: false);
    }

    private static ProjectionRebuildCheckpointStore CreateStore(DaprClient daprClient)
        => new(
            daprClient,
            Options.Create(new ProjectionOptions { CheckpointStateStoreName = "statestore" }),
            NullLogger<ProjectionRebuildCheckpointStore>.Instance);

    private static ProjectionRebuildCheckpoint CreateCheckpoint(long lastAppliedSequence, ProjectionRebuildStatus status)
        => new(
            AggregateScope.Tenant,
            AggregateScope.Domain,
            AggregateScope.ProjectionName,
            AggregateScope.AggregateId,
            AggregateScope.OperationId,
            lastAppliedSequence,
            status,
            DateTimeOffset.UtcNow,
            null);

    private static void SetupDelete(DaprClient daprClient, string etag, bool result)
        => _ = daprClient.TryDeleteStateAsync(
                "statestore",
                AggregateKey,
                etag,
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(result);

    private static void SetupReadEtag(
        DaprClient daprClient,
        ProjectionRebuildCheckpoint? value,
        string etag)
        => _ = daprClient.GetStateAndETagAsync<ProjectionRebuildCheckpoint>(
                "statestore",
                AggregateKey,
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(((ProjectionRebuildCheckpoint, string))(value!, etag));
}
