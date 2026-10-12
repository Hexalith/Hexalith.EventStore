using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Snapshots;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class AggregateActorManualSnapshotTests {
    [Theory]
    [InlineData("{\"nested\":{\"count\":2,\"Count\":3}}", false)]
    [InlineData("{\"items\":[{\"count\":2,\"Count\":3}]}", false)]
    [InlineData("{\"nested\":{\"count\":2},\"items\":[{\"count\":3}]}", true)]
    public async Task CreateManualSnapshotAsync_RecursiveCandidateValidationMatchesSnapshotRead(
        string stateJson, bool admitted)
    {
        // HX-FS-V1 §§5/9, V3/V7/V17: duplicates cannot become unreadable persisted authority.
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        var snapshots = new SnapshotManager(Options.Create(new SnapshotOptions()),
            Substitute.For<ILogger<SnapshotManager>>(), new NoOpEventPayloadProtectionService());
        IAggregateStateReconstructor reconstructor = Substitute.For<IAggregateStateReconstructor>();
        _ = reconstructor.ReconstructAsync(identity, "OrderAggregate", Arg.Any<IReadOnlyList<EventEnvelope>>(),
                2, false, "corr-nested", Arg.Any<CancellationToken>())
            .Returns(AggregateReconstructionResult.Succeeded(stateJson, 2));
        AggregateActor actor = CreateActor(identity, stateManager, snapshots, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-nested");

        if (!admitted)
        {
            result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
            result.ReasonCode.ShouldBe("SnapshotStateInvalid");
            stateManager.Trace.ShouldNotContain($"SetState:{identity.SnapshotKey}");
            stateManager.Trace.ShouldNotContain("SaveState");
            stateManager.CommittedState.ContainsKey(identity.SnapshotKey).ShouldBeFalse();
            return;
        }
        result.Outcome.ShouldBe(ManualSnapshotOutcome.Created);
        SnapshotLoadResult readback = await snapshots.LoadSnapshotForCommandAsync(identity, stateManager);
        readback.Outcome.ShouldBe(SnapshotLoadOutcome.Readable);
        SnapshotRecord persisted = stateManager.CommittedState[identity.SnapshotKey].ShouldBeOfType<SnapshotRecord>();
        persisted.SequenceNumber.ShouldBe(2);
        JsonElement.DeepEquals(readback.Snapshot.ShouldNotBeNull().State.ShouldBeOfType<JsonElement>(),
            JsonSerializer.Deserialize<JsonElement>(stateJson)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("SnapshotReadUnavailable")]
    [InlineData("SnapshotUnprotectUnavailable")]
    [InlineData("provider-secret-detail")]
    public async Task CreateManualSnapshotAsync_InspectionInfrastructureFailureRetainsKey(string reason)
    {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        ISnapshotManager snapshots = Substitute.For<ISnapshotManager>();
        _ = snapshots.InspectSnapshotForManualOverwriteAsync(identity, stateManager,
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SnapshotLoadResult.InfrastructureFailure(reason));
        IAggregateStateReconstructor reconstructor = Substitute.For<IAggregateStateReconstructor>();
        AggregateActor actor = CreateActor(identity, stateManager, snapshots, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-unavailable");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
        result.ReasonCode.ShouldBe(reason == "SnapshotUnprotectUnavailable"
            ? "SnapshotUnprotectUnavailable" : "SnapshotReadUnavailable");
        result.Message.ShouldNotContain("provider-secret-detail");
        stateManager.Trace.ShouldNotContain($"SetState:{identity.SnapshotKey}");
        stateManager.Trace.ShouldNotContain("SaveState");
        _ = await reconstructor.DidNotReceive().ReconstructAsync(
            Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(SnapshotLoadOutcome.UnreadableProtected, "SnapshotUnreadableProtected", "SnapshotUnreadableProtected", ManualSnapshotOutcome.UnreadableProtected)]
    [InlineData(SnapshotLoadOutcome.ProviderOpaque, "SnapshotProtectionOpaque", "SnapshotProtectionOpaque", ManualSnapshotOutcome.UnreadableProtected)]
    [InlineData(SnapshotLoadOutcome.ProviderOpaque, "SnapshotRecordUnavailable", "SnapshotRecordUnavailable", ManualSnapshotOutcome.UnreadableProtected)]
    [InlineData(SnapshotLoadOutcome.Corrupt, "SnapshotStateInvalid", "SnapshotStateInvalid", ManualSnapshotOutcome.InfrastructureFailure)]
    [InlineData(SnapshotLoadOutcome.Corrupt, "SnapshotSequenceInvalid", "SnapshotSequenceInvalid", ManualSnapshotOutcome.InfrastructureFailure)]
    [InlineData(SnapshotLoadOutcome.Corrupt, "CorruptSnapshot", "CorruptSnapshot", ManualSnapshotOutcome.UnreadableProtected)]
    [InlineData(SnapshotLoadOutcome.ProviderOpaque, "provider-secret-detail", "ProviderOpaque", ManualSnapshotOutcome.UnreadableProtected)]
    [InlineData(SnapshotLoadOutcome.Corrupt, null, "Corrupt", ManualSnapshotOutcome.UnreadableProtected)]
    public async Task CreateManualSnapshotAsync_UnreadableOrCorruptInspectionRetainsKey(
        SnapshotLoadOutcome outcome, string? reason, string expectedReasonClass, ManualSnapshotOutcome expectedOutcome)
    {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        SnapshotRecord retained = new(1, JsonSerializer.Deserialize<JsonElement>("""{"count":1}"""),
            DateTimeOffset.UnixEpoch, identity.Domain, identity.AggregateId, identity.TenantId);
        await stateManager.SetStateAsync(identity.SnapshotKey, retained);
        await stateManager.SaveStateAsync();
        int writesBefore = stateManager.Trace.Count(entry => entry == $"SetState:{identity.SnapshotKey}");
        int savesBefore = stateManager.Trace.Count(entry => entry == "SaveState");
        ISnapshotManager snapshots = Substitute.For<ISnapshotManager>();
        _ = snapshots.InspectSnapshotForManualOverwriteAsync(identity, stateManager,
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new SnapshotLoadResult(outcome, null, reason));
        IAggregateStateReconstructor reconstructor = Substitute.For<IAggregateStateReconstructor>();
        AggregateActor actor = CreateActor(identity, stateManager, snapshots, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-retain");

        result.Outcome.ShouldBe(expectedOutcome);
        result.ReasonCode.ShouldBe(expectedReasonClass);
        result.Message.ShouldBe($"Existing snapshot cannot be safely read: {expectedReasonClass}.");
        stateManager.Trace.Count(entry => entry == $"SetState:{identity.SnapshotKey}").ShouldBe(writesBefore);
        stateManager.Trace.Count(entry => entry == "SaveState").ShouldBe(savesBefore);
        stateManager.CommittedState[identity.SnapshotKey].ShouldBe(retained);
        _ = await reconstructor.DidNotReceive().ReconstructAsync(
            Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_WrongAddressRetainsKeyWithSafeReason()
    {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        JsonElement retained = JsonSerializer.Deserialize<JsonElement>(
            """{"snapshotEnvelopeVersion":1,"sequenceNumber":1,"state":{"count":1},"createdAt":"2026-01-01T00:00:00Z","domain":"orders","aggregateId":"other-order","tenantId":"tenant-a"}""");
        await stateManager.SetStateAsync(identity.SnapshotKey, retained);
        await stateManager.SaveStateAsync();
        int writesBefore = stateManager.Trace.Count(entry => entry == $"SetState:{identity.SnapshotKey}");
        int savesBefore = stateManager.Trace.Count(entry => entry == "SaveState");
        var snapshots = new SnapshotManager(Options.Create(new SnapshotOptions()),
            Substitute.For<ILogger<SnapshotManager>>(), new NoOpEventPayloadProtectionService());
        IAggregateStateReconstructor reconstructor = Substitute.For<IAggregateStateReconstructor>();
        AggregateActor actor = CreateActor(identity, stateManager, snapshots, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-address-mismatch");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.UnreadableProtected);
        result.ReasonCode.ShouldBe("AddressMismatch");
        result.Message.ShouldBe("Existing snapshot cannot be safely read: AddressMismatch.");
        stateManager.Trace.Count(entry => entry == $"SetState:{identity.SnapshotKey}").ShouldBe(writesBefore);
        stateManager.Trace.Count(entry => entry == "SaveState").ShouldBe(savesBefore);
        JsonElement.DeepEquals(stateManager.CommittedState[identity.SnapshotKey].ShouldBeOfType<JsonElement>(),
            retained).ShouldBeTrue();
        _ = await reconstructor.DidNotReceive().ReconstructAsync(
            Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1, ManualSnapshotOutcome.Created)]
    [InlineData(2, ManualSnapshotOutcome.AlreadyCurrent)]
    public async Task CreateManualSnapshotAsync_OnlyEqualFoldedSnapshotIsAlreadyCurrent(
        long existingSequence, ManualSnapshotOutcome expected)
    {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        ISnapshotManager snapshots = CreateStagingSnapshotManager(identity, stateManager);
        SnapshotRecord existing = new(existingSequence,
            JsonSerializer.Deserialize<JsonElement>("""{"status":"old"}"""),
            DateTimeOffset.UnixEpoch, identity.Domain, identity.AggregateId, identity.TenantId)
            { SnapshotEnvelopeVersion = 1 };
        _ = snapshots.InspectSnapshotForManualOverwriteAsync(identity, stateManager,
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SnapshotLoadResult.Readable(existing));
        IAggregateStateReconstructor reconstructor = CreateReconstructor(identity);
        AggregateActor actor = CreateActor(identity, stateManager, snapshots, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-ambiguous");

        result.Outcome.ShouldBe(expected);
        if (expected == ManualSnapshotOutcome.AlreadyCurrent)
        {
            stateManager.Trace.ShouldNotContain($"SetState:{identity.SnapshotKey}");
            stateManager.Trace.ShouldNotContain("SaveState");
            _ = await reconstructor.DidNotReceive().ReconstructAsync(
                Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<EventEnvelope>>(),
                Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        }
        else
        {
            ((SnapshotRecord)stateManager.CommittedState[identity.SnapshotKey]).SequenceNumber.ShouldBe(2);
            stateManager.Trace.ShouldContain("SaveState");
            int saved = stateManager.Trace.FindIndex(entry => entry == "SaveState:Delegated");
            saved.ShouldBeGreaterThanOrEqualTo(0);
            int cleared = stateManager.Trace.FindIndex(saved + 1, entry => entry == "ClearCache");
            int readback = stateManager.Trace.FindIndex(saved + 1,
                entry => entry == $"TryGetState:{identity.SnapshotKey}");
            cleared.ShouldBeGreaterThan(saved);
            readback.ShouldBeGreaterThan(cleared);
        }
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_ProviderMessageWithSnapshotPrefixIsNotReturned()
    {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        ISnapshotManager snapshots = Substitute.For<ISnapshotManager>();
        _ = snapshots.InspectSnapshotForManualOverwriteAsync(identity, stateManager,
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SnapshotLoadResult.Absent());
        IAggregateStateReconstructor reconstructor = Substitute.For<IAggregateStateReconstructor>();
        _ = reconstructor.ReconstructAsync(identity, "OrderAggregate", Arg.Any<IReadOnlyList<EventEnvelope>>(),
                2, false, "corr-sensitive", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AggregateReconstructionResult>(
                new InvalidOperationException("Snapshot private provider detail")));
        var logger = new SnapshotCaptureLogger();
        AggregateActor actor = CreateActor(identity, stateManager, snapshots, reconstructor,
            logger: logger);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-sensitive");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
        result.ReasonCode.ShouldBe("InfrastructureFailure");
        result.Message.ShouldNotBeNull().ShouldContain("InfrastructureFailure");
        result.Message.ShouldNotContain("Snapshot private provider detail");
        logger.Messages.ShouldContain(message => message.Contains(
            "FailureClass=InfrastructureFailure", StringComparison.Ordinal));
        logger.Exceptions.ShouldAllBe(exception => exception == null);
        logger.Messages.ShouldNotContain(message => message.Contains(
            "Snapshot private provider detail", StringComparison.Ordinal));
        stateManager.Trace.ShouldNotContain($"SetState:{identity.SnapshotKey}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("StaleAggregate")]
    public async Task CreateManualSnapshotAsync_AbsentOrStaleQualificationFailsBeforeReconstruction(
        string? qualifiedAggregateType) {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        ISnapshotManager snapshots = Substitute.For<ISnapshotManager>();
        _ = snapshots.InspectSnapshotForManualOverwriteAsync(identity, stateManager,
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SnapshotLoadResult.Absent());
        IAggregateStateReconstructor reconstructor = Substitute.For<IAggregateStateReconstructor>();
        AggregateActor actor = CreateActor(identity, stateManager, snapshots, reconstructor,
            qualifiedAggregateType: qualifiedAggregateType);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-unsupported");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
        result.ReasonCode.ShouldBe("SnapshotCaptureUnsupported");
        _ = await reconstructor.DidNotReceive().ReconstructAsync(
            Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        stateManager.Trace.ShouldNotContain($"SetState:{identity.SnapshotKey}");
        stateManager.Trace.ShouldNotContain("SaveState");
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_MissingFirstEventReportsUnavailableAuthoritativePrefix()
    {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await stateManager.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [identity.MetadataKey] = new AggregateMetadata(2, DateTimeOffset.UtcNow, null),
            [$"{identity.EventStreamKeyPrefix}2"] = CreateEvent(identity, 2),
        });
        ISnapshotManager snapshots = Substitute.For<ISnapshotManager>();
        _ = snapshots.InspectSnapshotForManualOverwriteAsync(identity, stateManager,
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SnapshotLoadResult.Absent());
        IAggregateStateReconstructor reconstructor = Substitute.For<IAggregateStateReconstructor>();
        AggregateActor actor = CreateActor(identity, stateManager, snapshots, reconstructor,
            qualifiedAggregateType: null);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-missing-prefix");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
        result.ReasonCode.ShouldBe("ReplayRestartRequired");
        result.SequenceNumber.ShouldBe(2);
        _ = await reconstructor.DidNotReceive().ReconstructAsync(
            Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        stateManager.Trace.ShouldNotContain($"SetState:{identity.SnapshotKey}");
        stateManager.Trace.ShouldNotContain("SaveState");
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_DscsCandidateFailsWithoutOverwritingRetainedSnapshot() {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        JsonElement retained = JsonSerializer.Deserialize<JsonElement>(
            """{"sequenceNumber":1,"state":{"currentSequence":1,"events":[],"snapshotState":{"status":"old"}},"createdAt":"2026-01-01T00:00:00Z","domain":"orders","aggregateId":"order-1","tenantId":"tenant-a"}""");
        await stateManager.SetStateAsync(identity.SnapshotKey, retained);
        await stateManager.SaveStateAsync();
        int snapshotWritesBefore = stateManager.Trace.Count(operation => operation == $"SetState:{identity.SnapshotKey}");
        int savesBefore = stateManager.Trace.Count(operation => operation == "SaveState");
        var snapshots = new SnapshotManager(Options.Create(new SnapshotOptions()),
            Substitute.For<ILogger<SnapshotManager>>(), new NoOpEventPayloadProtectionService());
        IAggregateStateReconstructor reconstructor = Substitute.For<IAggregateStateReconstructor>();
        _ = reconstructor.ReconstructAsync(identity, "OrderAggregate", Arg.Any<IReadOnlyList<EventEnvelope>>(),
                2, false, "corr-dscs", Arg.Any<CancellationToken>())
            .Returns(AggregateReconstructionResult.Succeeded(
                """{"currentSequence":2,"events":[],"snapshotState":{"status":"new"}}""", 2));
        AggregateActor actor = CreateActor(identity, stateManager, snapshots, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-dscs");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
        result.ReasonCode.ShouldBe("SnapshotStateShapeCollision");
        stateManager.Trace.Count(operation => operation == $"SetState:{identity.SnapshotKey}")
            .ShouldBe(snapshotWritesBefore);
        stateManager.Trace.Count(operation => operation == "SaveState").ShouldBe(savesBefore);
        JsonElement.DeepEquals(stateManager.CommittedState[identity.SnapshotKey].ShouldBeOfType<JsonElement>(), retained)
            .ShouldBeTrue();
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_ShapeCollisionCanMigrateOnlyAfterValidFold()
    {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        JsonElement retained = JsonSerializer.Deserialize<JsonElement>(
            """{"sequenceNumber":1,"state":{"currentSequence":1,"events":[]},"createdAt":"2026-01-01T00:00:00Z","domain":"orders","aggregateId":"order-1","tenantId":"tenant-a"}""");
        await stateManager.SetStateAsync(identity.SnapshotKey, retained);
        await stateManager.SaveStateAsync();
        var snapshots = new SnapshotManager(Options.Create(new SnapshotOptions()),
            Substitute.For<ILogger<SnapshotManager>>(), new NoOpEventPayloadProtectionService());
        IAggregateStateReconstructor reconstructor = Substitute.For<IAggregateStateReconstructor>();
        _ = reconstructor.ReconstructAsync(identity, "OrderAggregate", Arg.Any<IReadOnlyList<EventEnvelope>>(),
                2, false, "corr-migrate", Arg.Any<CancellationToken>())
            .Returns(AggregateReconstructionResult.Succeeded("""{"status":"ready"}""", 2));
        AggregateActor actor = CreateActor(identity, stateManager, snapshots, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-migrate");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.Created);
        SnapshotRecord replacement = stateManager.CommittedState[identity.SnapshotKey].ShouldBeOfType<SnapshotRecord>();
        replacement.SequenceNumber.ShouldBe(2);
        replacement.State.ShouldBeOfType<JsonElement>().GetProperty("status").GetString().ShouldBe("ready");
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_FutureDscsSequenceRetainsKeyAndRefusesReconstruction() {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        JsonElement retained = JsonSerializer.Deserialize<JsonElement>(
            """{"sequenceNumber":3,"state":{"currentSequence":3,"events":[]},"createdAt":"2026-01-01T00:00:00Z","domain":"orders","aggregateId":"order-1","tenantId":"tenant-a"}""");
        await stateManager.SetStateAsync(identity.SnapshotKey, retained);
        await stateManager.SaveStateAsync();
        int writesBefore = stateManager.Trace.Count(operation => operation == $"SetState:{identity.SnapshotKey}");
        IAggregateStateReconstructor reconstructor = Substitute.For<IAggregateStateReconstructor>();
        var snapshots = new SnapshotManager(Options.Create(new SnapshotOptions()),
            Substitute.For<ILogger<SnapshotManager>>(), new NoOpEventPayloadProtectionService());
        AggregateActor actor = CreateActor(identity, stateManager, snapshots, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-future-dscs");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
        result.ReasonCode.ShouldBe("SnapshotSequenceInvalid");
        stateManager.Trace.Count(operation => operation == $"SetState:{identity.SnapshotKey}").ShouldBe(writesBefore);
        _ = await reconstructor.DidNotReceive().ReconstructAsync(
            Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_StoresReconstructedState_NotReplayEnvelope() {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ConfigureStream(stateManager, identity);

        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        _ = snapshotManager.InspectSnapshotForManualOverwriteAsync(
                identity,
                stateManager,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(SnapshotLoadResult.Absent());

        object? capturedState = null;
        SnapshotRecord? stagedSnapshot = null;
        _ = snapshotManager.CreateSnapshotAsync(
                identity,
                2,
                Arg.Any<object>(),
                stateManager,
                Arg.Any<JsonSerializerOptions>(),
                Arg.Any<FoldedSnapshotCaptureLimits>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                true)
            .Returns(callInfo => {
                capturedState = callInfo.ArgAt<object>(2);
                stagedSnapshot = new SnapshotRecord(
                    2,
                    capturedState,
                    DateTimeOffset.UnixEpoch,
                    identity.Domain,
                    identity.AggregateId,
                    identity.TenantId) { SnapshotEnvelopeVersion = 1 };
                return Task.CompletedTask;
            });
        _ = stateManager.TryGetStateAsync<SnapshotRecord>(
                identity.SnapshotKey,
                Arg.Any<CancellationToken>())
            .Returns(_ => stagedSnapshot is null
                ? new ConditionalValue<SnapshotRecord>(false, default!)
                : new ConditionalValue<SnapshotRecord>(true, stagedSnapshot));

        IAggregateStateReconstructor reconstructor = Substitute.For<IAggregateStateReconstructor>();
        _ = reconstructor.ReconstructAsync(
                identity,
                "OrderAggregate",
                Arg.Any<IReadOnlyList<EventEnvelope>>(),
                2,
                false,
                "corr-1",
                Arg.Any<CancellationToken>())
            .Returns(AggregateReconstructionResult.Succeeded("""{"status":"ready","count":2}""", 2));

        AggregateActor actor = CreateActor(identity, stateManager, snapshotManager, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-1");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.Created);
        JsonElement state = capturedState.ShouldBeOfType<JsonElement>();
        state.TryGetProperty("status", out JsonElement status).ShouldBeTrue();
        status.GetString().ShouldBe("ready");
        capturedState.ShouldNotBeOfType<Hexalith.EventStore.Contracts.Commands.DomainServiceCurrentState>();
        await stateManager.Received(1).SaveStateAsync();
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_TimeoutDuringStagedReadDiscardsSnapshotBeforeSave()
    {
        // HX-FS-V1 §8.4/V11: expiry before commit must discard the staged batch.
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        IReadOnlyDictionary<string, object> committedBefore = stateManager.CreateCommittedView();
        ISnapshotManager snapshots = CreateStagingSnapshotManager(identity, stateManager);
        CancellationToken manualTimeoutToken = default;
        _ = snapshots.InspectSnapshotForManualOverwriteAsync(identity, stateManager,
                Arg.Any<string?>(), Arg.Do<CancellationToken>(token => manualTimeoutToken = token))
            .Returns(SnapshotLoadResult.Absent());
        stateManager.ActBeforeCall($"TryGetState:{identity.SnapshotKey}", 1, async manager =>
        {
            manualTimeoutToken.CanBeCanceled.ShouldBeTrue();
            manualTimeoutToken.IsCancellationRequested.ShouldBeFalse();
            (await manager.ContainsStateAsync(identity.SnapshotKey)).ShouldBeTrue();
            var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenRegistration registration = manualTimeoutToken.Register(() => expired.SetResult());
            // Complete the cached read after the real manual timeout expires. The read
            // itself succeeds, so only the final pre-save cancellation check can refuse it.
            await expired.Task.WaitAsync(TimeSpan.FromSeconds(45));
            manualTimeoutToken.IsCancellationRequested.ShouldBeTrue();
        });
        AggregateActor actor = CreateActor(identity, stateManager, snapshots, CreateReconstructor(identity));

        OperationCanceledException cancellation = await Should.ThrowAsync<OperationCanceledException>(
            () => actor.CreateManualSnapshotAsync("corr-ambiguous"));

        cancellation.CancellationToken.ShouldBe(manualTimeoutToken);
        stateManager.Trace.ShouldContain($"SetState:{identity.SnapshotKey}:Delegated");
        stateManager.Trace.ShouldContain($"TryGetState:{identity.SnapshotKey}:Delegated");
        stateManager.Trace.ShouldNotContain("SaveState");
        stateManager.Trace.ShouldContain("ClearCache:Delegated");
        (await stateManager.ContainsStateAsync(identity.SnapshotKey)).ShouldBeFalse();
        stateManager.CommittedState.Count.ShouldBe(committedBefore.Count);
        foreach ((string key, object value) in committedBefore)
        {
            stateManager.CommittedState[key].ShouldBe(value);
        }
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_SaveCommitsThenThrows_ReturnsCreatedFromFreshDurableWitness() {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        ISnapshotManager snapshotManager = CreateStagingSnapshotManager(identity, stateManager);
        IAggregateStateReconstructor reconstructor = CreateReconstructor(identity);
        stateManager.FaultAfterCall("SaveState", 1, new InvalidOperationException("commit uncertain"));
        AggregateActor actor = CreateActor(identity, stateManager, snapshotManager, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-ambiguous");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.Created);
        ((SnapshotRecord)stateManager.CommittedState[identity.SnapshotKey]).SequenceNumber.ShouldBe(2);
        stateManager.Trace.Count(operation => operation == "SaveState").ShouldBe(1);
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_PostSaveReadbackRejectsDifferentDurableRecord()
    {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        ISnapshotManager snapshotManager = CreateStagingSnapshotManager(identity, stateManager);
        IAggregateStateReconstructor reconstructor = CreateReconstructor(identity);
        stateManager.ActBeforeCall($"TryGetState:{identity.SnapshotKey}", 2, async manager =>
        {
            SnapshotRecord staged = ((SnapshotRecord)manager.CommittedState[identity.SnapshotKey]);
            await manager.InjectConcurrentWinnerAsync(new Dictionary<string, object>
            {
                [identity.SnapshotKey] = staged with { SnapshotEnvelopeVersion = 0 },
            });
        });
        AggregateActor actor = CreateActor(identity, stateManager, snapshotManager, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-ambiguous");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
        result.ReasonCode.ShouldBe("SnapshotReadbackMismatch");
        stateManager.Trace.ShouldContain("ClearCache");
        ((SnapshotRecord)stateManager.CommittedState[identity.SnapshotKey]).SnapshotEnvelopeVersion.ShouldBe(0);
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_SaveFailsBeforeCommit_ReturnsBoundedFailureAndDiscardsSnapshot() {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        ISnapshotManager snapshotManager = CreateStagingSnapshotManager(identity, stateManager);
        IAggregateStateReconstructor reconstructor = CreateReconstructor(identity);
        stateManager.FaultOnCall("SaveState", 1, new InvalidOperationException("pre-commit failure"));
        AggregateActor actor = CreateActor(identity, stateManager, snapshotManager, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-ambiguous");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
        stateManager.CommittedState.ShouldNotContainKey(identity.SnapshotKey);
        stateManager.Trace.ShouldContain("ClearCache");
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_AmbiguousSaveWithConcurrentSameSequenceReplacement_DoesNotClaimCreated() {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        ISnapshotManager snapshotManager = CreateStagingSnapshotManager(identity, stateManager);
        IAggregateStateReconstructor reconstructor = CreateReconstructor(identity);
        var winner = new SnapshotRecord(
            2,
            JsonSerializer.Deserialize<JsonElement>("""{"status":"concurrent-winner"}"""),
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            identity.Domain,
            identity.AggregateId,
            identity.TenantId);
        stateManager.FaultAfterCall("SaveState", 1, new InvalidOperationException("commit uncertain"));
        stateManager.ActBeforeCall(
            $"TryGetState:{identity.SnapshotKey}",
            2,
            manager => manager.InjectConcurrentWinnerAsync(new Dictionary<string, object>
            {
                [identity.SnapshotKey] = winner,
            }));
        AggregateActor actor = CreateActor(identity, stateManager, snapshotManager, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-ambiguous");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
        ((SnapshotRecord)stateManager.CommittedState[identity.SnapshotKey]).ShouldBe(winner);
        stateManager.Trace.ShouldContain("ConcurrentWinner");
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_AmbiguousVersionOnlyReplacementDoesNotClaimCreated() {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        ISnapshotManager snapshotManager = CreateStagingSnapshotManager(identity, stateManager);
        stateManager.FaultAfterCall("SaveState", 1, new InvalidOperationException("commit uncertain"));
        stateManager.ActBeforeCall(
            $"TryGetState:{identity.SnapshotKey}",
            2,
            manager => manager.InjectConcurrentWinnerAsync(new Dictionary<string, object> {
                [identity.SnapshotKey] = ((SnapshotRecord)manager.CommittedState[identity.SnapshotKey])
                    with { SnapshotEnvelopeVersion = 0 },
            }));
        AggregateActor actor = CreateActor(identity, stateManager, snapshotManager, CreateReconstructor(identity));

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-ambiguous");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
        ((SnapshotRecord)stateManager.CommittedState[identity.SnapshotKey]).SnapshotEnvelopeVersion.ShouldBe(0);
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_RenamedEnvelopeSerializerIsUnsupported() {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        _ = snapshotManager.InspectSnapshotForManualOverwriteAsync(
                identity,
                stateManager,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(SnapshotLoadResult.Absent());
        var actorStateOptions = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        AggregateActor actor = CreateActor(
            identity,
            stateManager,
            snapshotManager,
            CreateReconstructor(identity),
            actorStateOptions);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-ambiguous");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
        result.ReasonCode.ShouldBe("SnapshotSerializerUnsupported");
        stateManager.Trace.ShouldNotContain("SaveState");
    }

    private static ISnapshotManager CreateStagingSnapshotManager(
        AggregateIdentity identity,
        FaultInjectingActorStateManager stateManager) {
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        _ = snapshotManager.InspectSnapshotForManualOverwriteAsync(
                identity,
                stateManager,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(SnapshotLoadResult.Absent());
        _ = snapshotManager.CreateSnapshotAsync(
                identity,
                2,
                Arg.Any<object>(),
                stateManager,
                Arg.Any<JsonSerializerOptions>(),
                Arg.Any<FoldedSnapshotCaptureLimits>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                true)
            .Returns(callInfo => stateManager.SetStateAsync(
                identity.SnapshotKey,
                new SnapshotRecord(
                    2,
                    callInfo.ArgAt<object>(2),
                    DateTimeOffset.UnixEpoch,
                    identity.Domain,
                    identity.AggregateId,
                    identity.TenantId) { SnapshotEnvelopeVersion = 1 }));
        return snapshotManager;
    }

    private static IAggregateStateReconstructor CreateReconstructor(AggregateIdentity identity) {
        IAggregateStateReconstructor reconstructor = Substitute.For<IAggregateStateReconstructor>();
        _ = reconstructor.ReconstructAsync(
                identity,
                "OrderAggregate",
                Arg.Any<IReadOnlyList<EventEnvelope>>(),
                2,
                false,
                "corr-ambiguous",
                Arg.Any<CancellationToken>())
            .Returns(AggregateReconstructionResult.Succeeded("""{"status":"ready"}""", 2));
        return reconstructor;
    }

    /// <summary>Verifies keyed logical history refusal produces a safe reason without staging a snapshot.</summary>
    [Fact]
    public async Task CreateManualSnapshotAsync_RejectedLogicalHistoryDoesNotSnapshotStoredPayloads() {
        var identity = new AggregateIdentity("tenant", "d", "aggregate");
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventEnvelope stored = new(
            MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
            AggregateId: identity.AggregateId,
            AggregateType: "r",
            TenantId: identity.TenantId,
            Domain: identity.Domain,
            SequenceNumber: 1,
            GlobalPosition: 0,
            Timestamp: DateTimeOffset.UnixEpoch,
            CorrelationId: "correlation",
            CausationId: "causation",
            UserId: "user",
            DomainServiceVersion: "v1",
            EventTypeName: "Legacy.Event",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: [1, 2],
            Extensions: null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "etag")));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, stored));

        using EventEvolutionManifestCandidate candidate = DaprProductionLogicalEventReaderTests.CreateUpcastingCandidate();
        using var handler = new CanonicalLogicalReplayHandler();
        using var httpClient = new HttpClient(handler);
        using var dapr = new Dapr.Client.DaprClientBuilder().Build();
        var reconstructor = DaprProductionLogicalEventReaderTests.CreateCanonicalReconstructor(dapr, httpClient, out _);
        var snapshotManager = new SnapshotManager(
            Options.Create(new SnapshotOptions()),
            Substitute.For<ILogger<SnapshotManager>>(),
            new NoOpEventPayloadProtectionService());
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(identity.ActorId) });
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var actor = new AggregateActor(
            host,
            logger,
            Substitute.For<IDomainServiceInvoker>(),
            snapshotManager,
            new NoOpEventPayloadProtectionService(),
            Substitute.For<ICommandStatusStore>(),
            Substitute.For<IEventPublisher>(),
            Options.Create(new EventDrainOptions()),
            Options.Create(new BackpressureOptions()),
            Substitute.For<IDeadLetterPublisher>(),
            new KeyedEvolutionProvider(candidate, reconstructor));
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-rejected");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
        result.ReasonCode.ShouldBe("logical-event-read-rejected");
        stored.Payload.ShouldBe([1, 2]);
        stateManager.ReceivedCalls().Any(call =>
            call.GetMethodInfo().Name is "SaveStateAsync" or "SetStateAsync").ShouldBeFalse();
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies keyed manual snapshots persist canonical plaintext state after one addressed range read.</summary>
    [Fact]
    public async Task CreateManualSnapshotAsync_KeyedV1HistoryReconstructsCanonicalStateWithOneRangeRead() {
        var identity = new AggregateIdentity("tenant", "d", "aggregate");
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventEnvelope[] stored = DaprProductionLogicalEventReaderTests.StoreCanonicalHistory(stateManager);
        using EventEvolutionManifestCandidate candidate = DaprProductionLogicalEventReaderTests.CreateCandidate();
        IEventPayloadProtectionService protection = DaprProductionLogicalEventReaderTests.CreateCanonicalProtection();
        using var handler = new CanonicalLogicalReplayHandler();
        using var httpClient = new HttpClient(handler);
        using var dapr = new Dapr.Client.DaprClientBuilder().Build();
        var reconstructor = DaprProductionLogicalEventReaderTests.CreateCanonicalReconstructor(dapr, httpClient, out _);
        var snapshots = new SnapshotManager(Options.Create(new SnapshotOptions()), Substitute.For<ILogger<SnapshotManager>>(),
            new NoOpEventPayloadProtectionService());
        SnapshotRecord? snapshot = null;
        _ = stateManager.SetStateAsync(identity.SnapshotKey, Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>())
            .Returns(call => { snapshot = call.ArgAt<SnapshotRecord>(1); return Task.CompletedTask; });
        _ = stateManager.TryGetStateAsync<SnapshotRecord>(identity.SnapshotKey, Arg.Any<CancellationToken>())
            .Returns(_ => snapshot is null ? new ConditionalValue<SnapshotRecord>(false, default!) : new ConditionalValue<SnapshotRecord>(true, snapshot));
        var host = ActorHost.CreateForTest<AggregateActor>(new ActorTestOptions { ActorId = new ActorId(identity.ActorId) });
        var actor = new AggregateActor(host, Substitute.For<ILogger<AggregateActor>>(), Substitute.For<IDomainServiceInvoker>(),
            snapshots, protection, Substitute.For<ICommandStatusStore>(), Substitute.For<IEventPublisher>(),
            Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()),
            Substitute.For<IDeadLetterPublisher>(), new KeyedEvolutionProvider(candidate, reconstructor));
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-canonical");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.Created);
        result.ReasonCode.ShouldBeNull();
        SnapshotRecord committed = snapshot.ShouldNotBeNull();
        committed.SequenceNumber.ShouldBe(3);
        committed.State.ShouldBeOfType<JsonElement>().GetProperty("count").GetInt32().ShouldBe(6);
        handler.Requests.ShouldHaveSingleItem().Events.Count.ShouldBe(3);
        // Qualification reads the first stored event before the normal addressed replay range.
        _ = await stateManager.Received(3).TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>());
        _ = await stateManager.Received(1).TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}2", Arg.Any<CancellationToken>());
        _ = await stateManager.Received(1).TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}3", Arg.Any<CancellationToken>());
        _ = await protection.Received(3).TryUnprotectEventPayloadAsync(identity, Arg.Any<string>(), Arg.Any<byte[]>(), "json",
            Arg.Any<EventStorePayloadProtectionMetadata>(), Arg.Any<CancellationToken>());
        stored.All(e => e.Payload.SequenceEqual(new byte[] { 9, (byte)e.SequenceNumber })).ShouldBeTrue();
        await stateManager.Received(1).SaveStateAsync();
    }

    private static Task SeedStreamAsync(
        FaultInjectingActorStateManager stateManager,
        AggregateIdentity identity)
        => stateManager.SeedCommittedStateAsync(new Dictionary<string, object> {
            [identity.MetadataKey] = new AggregateMetadata(2, DateTimeOffset.UtcNow, null),
            [$"{identity.EventStreamKeyPrefix}1"] = CreateEvent(identity, 1),
            [$"{identity.EventStreamKeyPrefix}2"] = CreateEvent(identity, 2),
        });

    private static AggregateActor CreateActor(
        AggregateIdentity identity,
        IActorStateManager stateManager,
        ISnapshotManager snapshotManager,
        IAggregateStateReconstructor reconstructor,
        JsonSerializerOptions? actorStateJsonSerializerOptions = null,
        string? qualifiedAggregateType = "OrderAggregate",
        ILogger<AggregateActor>? logger = null) {
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(identity.ActorId) });
        logger ??= Substitute.For<ILogger<AggregateActor>>();
        if (logger is not SnapshotCaptureLogger)
        {
            _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        }

        var actor = new AggregateActor(
            host,
            logger,
            Substitute.For<IDomainServiceInvoker>(),
            snapshotManager,
            new NoOpEventPayloadProtectionService(),
            Substitute.For<ICommandStatusStore>(),
            Substitute.For<IEventPublisher>(),
            Options.Create(new EventDrainOptions()),
            Options.Create(new BackpressureOptions()),
            Substitute.For<IDeadLetterPublisher>(),
            new TestServiceProvider(reconstructor, actorStateJsonSerializerOptions, identity.Domain,
                qualifiedAggregateType));

        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        return actor;
    }

    private static void ConfigureStream(IActorStateManager stateManager, AggregateIdentity identity) {
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(2, DateTimeOffset.UtcNow, null)));

        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, CreateEvent(identity, 1)));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, CreateEvent(identity, 2)));
    }

    private static EventEnvelope CreateEvent(AggregateIdentity identity, int sequence)
        => new(
            MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
            AggregateId: identity.AggregateId,
            AggregateType: "OrderAggregate",
            TenantId: identity.TenantId,
            Domain: identity.Domain,
            SequenceNumber: sequence,
            GlobalPosition: sequence,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-1",
            CausationId: $"cause-{sequence}",
            UserId: "operator",
            DomainServiceVersion: "v1",
            EventTypeName: "OrderChanged",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: JsonSerializer.SerializeToUtf8Bytes(new { sequence }),
            Extensions: null);

    private sealed class TestServiceProvider(
        IAggregateStateReconstructor reconstructor,
        JsonSerializerOptions? actorStateJsonSerializerOptions,
        string domain,
        string? qualifiedAggregateType) : IServiceProvider {
        private readonly FoldedSnapshotQualificationRegistry? _snapshotQualifications =
            qualifiedAggregateType is null ? null : new([
                new FoldedSnapshotQualification(domain, qualifiedAggregateType, typeof(JsonElement),
                    "test-profile", "test-parity", SnapshotTestLimits.ForState()),
            ]);
        private readonly IOptions<ActorRuntimeOptions>? _actorOptions = actorStateJsonSerializerOptions is null
            ? null
            : Options.Create(new ActorRuntimeOptions {
                JsonSerializerOptions = actorStateJsonSerializerOptions,
            });

        public object? GetService(Type serviceType)
            => serviceType == typeof(IAggregateStateReconstructor)
                ? reconstructor
                : serviceType == typeof(FoldedSnapshotQualificationRegistry)
                    ? _snapshotQualifications
                : serviceType == typeof(IOptions<ActorRuntimeOptions>)
                    ? _actorOptions
                    : null;
    }

}
