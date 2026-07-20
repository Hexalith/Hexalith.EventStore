using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class IdempotencyAdmissionActorTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Coordinator_RoutesOnlyProtectedIdentityAndIntentToAdmissionActor()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor proxy = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        IIdempotencyTenantLifecycleActor lifecycle = Substitute.For<IIdempotencyTenantLifecycleActor>();
        IIdempotencyLegacyInventoryActor legacyInventory = Substitute.For<IIdempotencyLegacyInventoryActor>();
        IdempotencyAdmissionRequest? capturedRequest = null;
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(proxy);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directory);
        _ = factory.CreateActorProxy<IIdempotencyTenantLifecycleActor>(
                Arg.Any<ActorId>(),
                IdempotencyTenantLifecycleActor.ActorTypeName)
            .Returns(lifecycle);
        _ = factory.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyLegacyInventoryActor.ActorTypeName)
            .Returns(legacyInventory);
        _ = legacyInventory.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>())
            .Returns(new IdempotencyLegacyInventoryInspection(IdempotencyLegacyInventoryDecision.NoLegacy));
        _ = proxy.InspectAsync().Returns(new IdempotencyAdmissionInspection(false));
        _ = directory.ResolveAsync(Arg.Any<IdempotencyAdmissionDirectoryRequest>())
            .Returns(callInfo =>
            {
                IdempotencyAdmissionDirectoryRequest request = callInfo
                    .ArgAt<IdempotencyAdmissionDirectoryRequest>(0);
                return new IdempotencyAdmissionDirectoryResult(
                    request.ActiveActorId,
                    IdempotencyAdmissionPromotionPhase.Stable);
            });
        _ = proxy.AdmitAsync(Arg.Any<IdempotencyAdmissionRequest>())
            .Returns(callInfo =>
            {
                capturedRequest = callInfo.ArgAt<IdempotencyAdmissionRequest>(0);
                return new IdempotencyAdmissionResult(
                    IdempotencyAdmissionDecision.Execute,
                    1,
                    ExecutionMessageId: capturedRequest.ExecutionMessageId,
                    ExecutionCorrelationId: capturedRequest.ExecutionCorrelationId);
            });
        var command = new Hexalith.EventStore.Server.Pipeline.Commands.SubmitCommand(
            MessageId: "01J00000000000000000000000",
            Tenant: "tenant-a",
            Domain: "folders",
            AggregateId: "folder-a",
            CommandType: "CreateFolderCommand",
            Payload: [1],
            CorrelationId: "trace-a",
            UserId: "user-a",
            IdempotencyKey: "opaque-secret-key");
        IIdempotencyIntentAdapterRegistry registry = Substitute.For<IIdempotencyIntentAdapterRegistry>();
        registry.Resolve(command).Returns(Descriptor("target-a"));
        var coordinator = new IdempotencyAdmissionCoordinator(
            factory,
            CreateProtector(),
            registry,
            CreateExecutionContextProtector());

        IdempotencyAdmissionSession session = (await coordinator.AdmitAsync(command)).ShouldNotBeNull();

        session.ActorId.ShouldNotContain("opaque-secret-key");
        session.Decision.ShouldBe(IdempotencyAdmissionDecision.Execute);
        JsonSerializer.Serialize(new { Session = session, Request = capturedRequest })
            .ShouldNotContain("opaque-secret-key");
        _ = factory.Received().CreateActorProxy<IIdempotencyAdmissionActor>(
            Arg.Is<ActorId>(actorId => !actorId.ToString().Contains("opaque-secret-key", StringComparison.Ordinal)),
            IdempotencyAdmissionActor.ActorTypeName);
        await proxy.Received(1).AdmitAsync(
            Arg.Is<IdempotencyAdmissionRequest>(request =>
                !request.KeyDigest.Contains("opaque-secret-key", StringComparison.Ordinal)
                && !request.VerificationTag.Contains("opaque-secret-key", StringComparison.Ordinal)
                && !request.IntentDigest.Contains("target-a", StringComparison.Ordinal)));
        await lifecycle.Received(1).RegisterAsync(
            Arg.Is<IdempotencyTenantLifecycleReference[]>(references =>
                references.Length == 1
                && !references[0].ActorId.Contains("opaque-secret-key", StringComparison.Ordinal)
                && !references[0].KeyDigest.Contains("opaque-secret-key", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Coordinator_UnknownTrustedAdapter_FailsBeforeActorAccess()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var coordinator = new IdempotencyAdmissionCoordinator(
            factory,
            CreateProtector(),
            new IdempotencyIntentAdapterRegistry([], new CanonicalIdempotencyIntentEncoder()));
        var command = new Hexalith.EventStore.Server.Pipeline.Commands.SubmitCommand(
            MessageId: "01J00000000000000000000000",
            Tenant: "tenant-a",
            Domain: "folders",
            AggregateId: "folder-a",
            CommandType: "UnregisteredCommand",
            Payload: [1],
            CorrelationId: "trace-a",
            UserId: "user-a",
            IdempotencyKey: "opaque-secret-key");

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => coordinator.AdmitAsync(command));

        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
            default!,
            default!);
    }

    [Fact]
    public async Task Coordinator_RetainedAuthority_PromotesBeforeAdmittingThroughActiveActor()
    {
        var calls = new List<string>();
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor source = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionActor target = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        IIdempotencyTenantLifecycleActor lifecycle = Substitute.For<IIdempotencyTenantLifecycleActor>();
        IIdempotencyLegacyInventoryActor legacyInventory = Substitute.For<IIdempotencyLegacyInventoryActor>();
        IdempotencyKeyProtector protector = CreateRotatingProtector();
        TrustedIdempotencyDescriptor descriptor = Descriptor("target-a");
        IdempotencyProtectedIdentitySet identities = await protector.ProtectAsync(
            "tenant-a",
            "opaque-secret-key",
            Descriptor("target-a"));
        IdempotencyProtectedIdentity active = identities.Active;
        IdempotencyProtectedIdentity retained = identities.Aliases[1];
        var replay = new CommandProcessingResult(
            true,
            CorrelationId: "original",
            EventCount: 1,
            ResultPayload: "same");
        IdempotencyAdmissionRecord retainedRecord = Record(
            state: IdempotencyAdmissionState.Terminal,
            intentDigest: retained.IntentDigest,
            replayResult: replay) with
        {
            DigestKeyVersion = retained.DigestKeyVersion,
            KeyDigest = retained.KeyDigest,
            VerificationTag = retained.VerificationTag,
        };
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Is<ActorId>(id => id.ToString() == retained.ActorId),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(source);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Is<ActorId>(id => id.ToString() == active.ActorId),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(target);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directory);
        _ = factory.CreateActorProxy<IIdempotencyTenantLifecycleActor>(
                Arg.Any<ActorId>(),
                IdempotencyTenantLifecycleActor.ActorTypeName)
            .Returns(lifecycle);
        _ = factory.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyLegacyInventoryActor.ActorTypeName)
            .Returns(legacyInventory);
        _ = legacyInventory.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>())
            .Returns(new IdempotencyLegacyInventoryInspection(IdempotencyLegacyInventoryDecision.NoLegacy));
        _ = source.InspectAsync().Returns(new IdempotencyAdmissionInspection(true, retainedRecord));
        _ = target.InspectAsync().Returns(new IdempotencyAdmissionInspection(false));
        _ = source.AdmitAsync(Arg.Any<IdempotencyAdmissionRequest>())
            .Returns(_ =>
            {
                calls.Add("classify");
                return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Replay, 7, replay);
            });
        _ = directory.ResolveAsync(Arg.Any<IdempotencyAdmissionDirectoryRequest>())
            .Returns(new IdempotencyAdmissionDirectoryResult(
                retained.ActorId,
                IdempotencyAdmissionPromotionPhase.PrepareTarget,
                retained.ActorId,
                active.ActorId));
        _ = target.PreparePromotionAsync(Arg.Any<IdempotencyAdmissionPromotionImportRequest>())
            .Returns(_ => { calls.Add("prepare"); return Task.CompletedTask; });
        _ = source.SetRedirectAsync(Arg.Any<IdempotencyAdmissionRedirectRequest>())
            .Returns(_ => { calls.Add("redirect"); return Task.CompletedTask; });
        _ = target.ActivatePromotionAsync(Arg.Any<IdempotencyAdmissionPromotionActivationRequest>())
            .Returns(_ => { calls.Add("activate"); return Task.CompletedTask; });
        _ = directory.AdvanceAsync(Arg.Any<IdempotencyAdmissionDirectoryAdvanceRequest>())
            .Returns(callInfo =>
            {
                IdempotencyAdmissionPromotionPhase completed = callInfo
                    .ArgAt<IdempotencyAdmissionDirectoryAdvanceRequest>(0).ExpectedPhase;
                calls.Add($"advance:{completed}");
                IdempotencyAdmissionPromotionPhase next = completed switch
                {
                    IdempotencyAdmissionPromotionPhase.PrepareTarget => IdempotencyAdmissionPromotionPhase.RedirectSource,
                    IdempotencyAdmissionPromotionPhase.RedirectSource => IdempotencyAdmissionPromotionPhase.FlipDirectory,
                    IdempotencyAdmissionPromotionPhase.FlipDirectory => IdempotencyAdmissionPromotionPhase.ActivateTarget,
                    IdempotencyAdmissionPromotionPhase.ActivateTarget => IdempotencyAdmissionPromotionPhase.Stable,
                    _ => throw new InvalidOperationException(),
                };
                return new IdempotencyAdmissionDirectoryResult(
                    next is IdempotencyAdmissionPromotionPhase.ActivateTarget or IdempotencyAdmissionPromotionPhase.Stable
                        ? active.ActorId
                        : retained.ActorId,
                    next,
                    next == IdempotencyAdmissionPromotionPhase.Stable ? null : retained.ActorId,
                    next == IdempotencyAdmissionPromotionPhase.Stable ? null : active.ActorId);
            });
        _ = target.AdmitAsync(Arg.Any<IdempotencyAdmissionRequest>())
            .Returns(_ =>
            {
                calls.Add("admit");
                return new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Replay, 7, replay);
            });
        IIdempotencyIntentAdapterRegistry registry = Substitute.For<IIdempotencyIntentAdapterRegistry>();
        var command = new Hexalith.EventStore.Server.Pipeline.Commands.SubmitCommand(
            "01J00000000000000000000000",
            "tenant-a",
            "folders",
            "folder-a",
            "CreateFolderCommand",
            [1],
            "trace-a",
            "user-a",
            IdempotencyKey: "opaque-secret-key");
        registry.Resolve(command).Returns(descriptor);
        var coordinator = new IdempotencyAdmissionCoordinator(factory, protector, registry);

        IdempotencyAdmissionSession session = (await coordinator.AdmitAsync(command)).ShouldNotBeNull();

        session.ActorId.ShouldBe(active.ActorId);
        session.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
        calls.ShouldBe([
            "classify",
            "prepare",
            "advance:PrepareTarget",
            "redirect",
            "advance:RedirectSource",
            "advance:FlipDirectory",
            "activate",
            "advance:ActivateTarget",
            "admit",
        ]);
    }

    [Fact]
    public async Task Coordinator_ExactLegacyInventory_PreparesActivatesAndRedirectsBeforeReplay()
    {
        var calls = new List<string>();
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor target = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        IIdempotencyTenantLifecycleActor lifecycle = Substitute.For<IIdempotencyTenantLifecycleActor>();
        IIdempotencyLegacyInventoryActor inventory = Substitute.For<IIdempotencyLegacyInventoryActor>();
        IdempotencyKeyProtector protector = CreateProtector();
        TrustedIdempotencyDescriptor descriptor = Descriptor("target-a");
        IdempotencyProtectedIdentity identity = (await protector.ProtectAsync(
            "tenant-a",
            "opaque-secret-key",
            Descriptor("target-a"))).Active;
        var replay = new CommandProcessingResult(
            true,
            CorrelationId: "trace-original",
            EventCount: 1,
            ResultPayload: "same");
        var entry = new IdempotencyLegacyInventoryEntry(
            IdempotencyLegacyInventoryEntry.CurrentSchemaVersion,
            "tenant-a",
            "tenant-a:folders:legacy-folder",
            "source-evidence-digest",
            1,
            identity.DigestKeyVersion,
            identity.KeyDigest,
            identity.VerificationTag,
            identity.IntentDigest,
            identity.RetentionTier,
            _now.AddDays(-1),
            _now,
            _now.AddDays(1),
            replay,
            "01J00000000000000000000000",
            "trace-original",
            IdempotencyLegacyMigrationPhase.Inventoried);
        IdempotencyAdmissionRecord? imported = null;
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(target);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directory);
        _ = factory.CreateActorProxy<IIdempotencyTenantLifecycleActor>(
                Arg.Any<ActorId>(),
                IdempotencyTenantLifecycleActor.ActorTypeName)
            .Returns(lifecycle);
        _ = factory.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyLegacyInventoryActor.ActorTypeName)
            .Returns(inventory);
        _ = inventory.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>())
            .Returns(new IdempotencyLegacyInventoryInspection(
                IdempotencyLegacyInventoryDecision.Migrate,
                entry));
        _ = target.PreparePromotionAsync(Arg.Any<IdempotencyAdmissionPromotionImportRequest>())
            .Returns(callInfo =>
            {
                imported = callInfo.ArgAt<IdempotencyAdmissionPromotionImportRequest>(0).Record;
                calls.Add("prepare");
                return Task.CompletedTask;
            });
        _ = inventory.AdvanceAsync(
                entry.DigestKeyVersion,
                entry.KeyDigest,
                Arg.Any<IdempotencyLegacyMigrationPhase>(),
                identity.ActorId)
            .Returns(callInfo =>
            {
                IdempotencyLegacyMigrationPhase expected = callInfo.ArgAt<IdempotencyLegacyMigrationPhase>(2);
                calls.Add(expected == IdempotencyLegacyMigrationPhase.Inventoried
                    ? "inventory-prepared"
                    : "inventory-migrated");
                return entry with
                {
                    Phase = expected == IdempotencyLegacyMigrationPhase.Inventoried
                        ? IdempotencyLegacyMigrationPhase.TargetPrepared
                        : IdempotencyLegacyMigrationPhase.Migrated,
                    TargetAdmissionActorId = identity.ActorId,
                };
            });
        _ = target.ActivatePromotionAsync(Arg.Any<IdempotencyAdmissionPromotionActivationRequest>())
            .Returns(_ => { calls.Add("activate"); return Task.CompletedTask; });
        _ = target.InspectAsync().Returns(_ => new IdempotencyAdmissionInspection(true, imported));
        _ = directory.ResolveAsync(Arg.Any<IdempotencyAdmissionDirectoryRequest>())
            .Returns(new IdempotencyAdmissionDirectoryResult(
                identity.ActorId,
                IdempotencyAdmissionPromotionPhase.Stable));
        _ = target.AdmitAsync(Arg.Any<IdempotencyAdmissionRequest>())
            .Returns(new IdempotencyAdmissionResult(
                IdempotencyAdmissionDecision.Replay,
                1,
                replay,
                ExecutionMessageId: entry.ExecutionMessageId,
                ExecutionCorrelationId: entry.ExecutionCorrelationId));
        var command = new Hexalith.EventStore.Server.Pipeline.Commands.SubmitCommand(
            entry.ExecutionMessageId,
            "tenant-a",
            "folders",
            "folder-a",
            "CreateFolderCommand",
            [1],
            "trace-current",
            "user-a",
            IdempotencyKey: "opaque-secret-key");
        IIdempotencyIntentAdapterRegistry registry = Substitute.For<IIdempotencyIntentAdapterRegistry>();
        registry.Resolve(command).Returns(descriptor);
        var coordinator = new IdempotencyAdmissionCoordinator(factory, protector, registry);

        IdempotencyAdmissionSession session = (await coordinator.AdmitAsync(command)).ShouldNotBeNull();

        session.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
        session.ReplayResult.ShouldBe(replay);
        imported.ShouldNotBeNull().State.ShouldBe(IdempotencyAdmissionState.Terminal);
        imported.IntentDigest.ShouldBe(identity.IntentDigest);
        calls.ShouldBe(["prepare", "inventory-prepared", "activate", "inventory-migrated"]);
    }

    [Fact]
    public async Task Protector_PartitionsByTenantAndKeyButComparesCanonicalIntent()
    {
        IdempotencyKeyProtector protector = CreateProtector();
        TrustedIdempotencyDescriptor first = Descriptor("target-a");
        TrustedIdempotencyDescriptor different = Descriptor("target-b");

        IdempotencyProtectedIdentity firstIdentity = (await protector.ProtectAsync("tenant-a", "opaque-secret-key", first)).Active;
        IdempotencyProtectedIdentity equivalentIdentity = (await protector.ProtectAsync("tenant-a", "opaque-secret-key", first)).Active;
        IdempotencyProtectedIdentity differentIntentIdentity = (await protector.ProtectAsync("tenant-a", "opaque-secret-key", different)).Active;
        IdempotencyProtectedIdentity otherTenantIdentity = (await protector.ProtectAsync("tenant-b", "opaque-secret-key", first)).Active;

        equivalentIdentity.ActorId.ShouldBe(firstIdentity.ActorId);
        equivalentIdentity.IntentDigest.ShouldBe(firstIdentity.IntentDigest);
        differentIntentIdentity.ActorId.ShouldBe(firstIdentity.ActorId);
        differentIntentIdentity.IntentDigest.ShouldNotBe(firstIdentity.IntentDigest);
        otherTenantIdentity.ActorId.ShouldNotBe(firstIdentity.ActorId);
        firstIdentity.ToString().ShouldNotContain("opaque-secret-key");
        firstIdentity.ActorId.ShouldNotContain("opaque-secret-key");
    }

    [Fact]
    public async Task AdmitAsync_NewKey_AtomicallyReservesAndIssuesFirstFence()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        IdempotencyAdmissionRequest request = Request();
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(false, default!));

        IdempotencyAdmissionResult result = await actor.AdmitAsync(request);

        result.Decision.ShouldBe(IdempotencyAdmissionDecision.Execute);
        result.FencingToken.ShouldBe(1);
        await stateManager.Received(1).SetStateAsync(
            IdempotencyAdmissionActor.StateName,
            Arg.Is<IdempotencyAdmissionRecord>(record =>
                record.State == IdempotencyAdmissionState.Reserved
                && record.FencingToken == 1
                && record.IntentDigest == request.IntentDigest),
            Arg.Any<CancellationToken>());
        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdmitAsync_EquivalentRetry_ReusesFirstWriterExecutionIdentityAndFence()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        IdempotencyAdmissionRecord existing = Record(state: IdempotencyAdmissionState.Pending);
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, existing));

        IdempotencyAdmissionResult result = await actor.AdmitAsync(Request(
            executionMessageId: "01J11111111111111111111111",
            executionCorrelationId: "trace-retry"));

        result.Decision.ShouldBe(IdempotencyAdmissionDecision.Pending);
        result.FencingToken.ShouldBe(existing.FencingToken);
        result.ExecutionMessageId.ShouldBe(existing.ExecutionMessageId);
        result.ExecutionCorrelationId.ShouldBe(existing.ExecutionCorrelationId);
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<IdempotencyAdmissionRecord>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BeginAsync_RecoverableResume_ReusesCurrentFence()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        IdempotencyAdmissionRecord recoverable = Record(state: IdempotencyAdmissionState.Recoverable);
        IdempotencyAdmissionRecord? pending = null;
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, recoverable));
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.StateName,
                Arg.Do<IdempotencyAdmissionRecord>(record => pending = record),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await actor.BeginAsync(new IdempotencyAdmissionTransitionRequest(recoverable.FencingToken));

        pending.ShouldNotBeNull().State.ShouldBe(IdempotencyAdmissionState.Pending);
        pending.FencingToken.ShouldBe(recoverable.FencingToken);
        pending.ExecutionMessageId.ShouldBe(recoverable.ExecutionMessageId);
        pending.ExecutionCorrelationId.ShouldBe(recoverable.ExecutionCorrelationId);
    }

    [Fact]
    public async Task AdmitAsync_LiveDifferentIntent_ReturnsConflictWithoutMutation()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        IdempotencyAdmissionRequest request = Request(intentDigest: "intent-b");
        IdempotencyAdmissionRecord existing = Record(
            state: IdempotencyAdmissionState.Terminal,
            intentDigest: "intent-a",
            replayResult: new CommandProcessingResult(true));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, existing));

        IdempotencyAdmissionResult result = await actor.AdmitAsync(request);

        result.Decision.ShouldBe(IdempotencyAdmissionDecision.Conflict);
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<IdempotencyAdmissionRecord>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdmitAsync_AtInclusiveExpiry_CompactsToMinimalTombstoneBeforeIntentComparison()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        IdempotencyAdmissionRequest request = Request(intentDigest: "different-intent");
        IdempotencyAdmissionRecord existing = Record(
            state: IdempotencyAdmissionState.Terminal,
            intentDigest: "original-intent",
            replayExpiresAt: _now,
            replayResult: new CommandProcessingResult(true, ResultPayload: "protected-result"));
        IdempotencyAdmissionTombstone? compacted = null;
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, existing));
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.TombstoneStateName,
                Arg.Do<IdempotencyAdmissionTombstone>(record => compacted = record),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        IdempotencyAdmissionResult result = await actor.AdmitAsync(request);

        result.Decision.ShouldBe(IdempotencyAdmissionDecision.Expired);
        compacted.ShouldNotBeNull().State.ShouldBe(IdempotencyAdmissionState.Expired);
        compacted.ReplayExpiredAt.ShouldBe(_now);
        compacted.TenantPartition.ShouldBe(request.TenantPartition);
        compacted.KeyDigest.ShouldBe(request.KeyDigest);
        compacted.VerificationTag.ShouldBe(request.VerificationTag);
        compacted.ToString().ShouldNotContain("protected-result");
        compacted.ToString().ShouldNotContain("original-intent");
        compacted.ToString().ShouldNotContain(existing.ExecutionMessageId!);
        typeof(IdempotencyAdmissionTombstone).GetProperty("FencingToken").ShouldBeNull();
        _ = await stateManager.Received(1).TryRemoveStateAsync(
            IdempotencyAdmissionActor.StateName,
            Arg.Any<CancellationToken>());
        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_CommitTierUsesSevenYearCalendarBoundary()
    {
        var leapDay = new DateTimeOffset(2024, 2, 29, 8, 30, 0, TimeSpan.Zero);
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, FakeTimeProvider timeProvider) = CreateActor(leapDay);
        IdempotencyAdmissionRecord pending = Record(
            state: IdempotencyAdmissionState.Pending,
            tier: IdempotencyReplayRetentionTier.Commit,
            firstConsumedAt: leapDay,
            lastObservedAt: leapDay);
        IdempotencyAdmissionRecord? terminal = null;
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, pending));
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.StateName,
                Arg.Do<IdempotencyAdmissionRecord>(record => terminal = record),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await actor.CompleteAsync(new IdempotencyAdmissionCompletionRequest(
            pending.FencingToken,
            new CommandProcessingResult(true, EventCount: 1)));

        terminal.ShouldNotBeNull().ReplayExpiresAt.ShouldBe(leapDay.AddYears(7));
        terminal.State.ShouldBe(IdempotencyAdmissionState.Terminal);
        timeProvider.GetUtcNow().ShouldBe(leapDay);
    }

    [Fact]
    public async Task CompleteAsync_MutationTierUsesExactlyTwentyFourHours()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        IdempotencyAdmissionRecord pending = Record(
            state: IdempotencyAdmissionState.Pending,
            lastObservedAt: _now,
            replayExpiresAt: null);
        IdempotencyAdmissionRecord? terminal = null;
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, pending));
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.StateName,
                Arg.Do<IdempotencyAdmissionRecord>(record => terminal = record),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await actor.CompleteAsync(new IdempotencyAdmissionCompletionRequest(
            pending.FencingToken,
            new CommandProcessingResult(true)));

        terminal.ShouldNotBeNull().ReplayExpiresAt.ShouldBe(_now.AddSeconds(86_400));
    }

    [Fact]
    public async Task AdmitAsync_ClockRollbackCannotResurrectExpiredTerminalRecord()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor(_now.AddHours(-2));
        IdempotencyAdmissionRecord existing = Record(
            state: IdempotencyAdmissionState.Terminal,
            lastObservedAt: _now,
            replayExpiresAt: _now,
            replayResult: new CommandProcessingResult(true));
        IdempotencyAdmissionTombstone? compacted = null;
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, existing));
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.TombstoneStateName,
                Arg.Do<IdempotencyAdmissionTombstone>(record => compacted = record),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        IdempotencyAdmissionResult result = await actor.AdmitAsync(Request());

        result.Decision.ShouldBe(IdempotencyAdmissionDecision.Expired);
        compacted.ShouldNotBeNull().LastObservedAt.ShouldBe(_now);
        compacted.State.ShouldBe(IdempotencyAdmissionState.Expired);
    }

    [Fact]
    public async Task AdmitAsync_OneTickBeforeExpiry_ReplaysWithoutCompaction()
    {
        DateTimeOffset beforeExpiry = _now.AddTicks(-1);
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor(beforeExpiry);
        var replay = new CommandProcessingResult(true, ResultPayload: "same");
        IdempotencyAdmissionRecord existing = Record(
            state: IdempotencyAdmissionState.Terminal,
            firstConsumedAt: _now.AddHours(-1),
            lastObservedAt: beforeExpiry,
            replayExpiresAt: _now,
            replayResult: replay);
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, existing));

        IdempotencyAdmissionResult result = await actor.AdmitAsync(Request());

        result.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
        result.ReplayResult.ShouldBe(replay);
        await stateManager.DidNotReceive().SetStateAsync(
            IdempotencyAdmissionActor.TombstoneStateName,
            Arg.Any<IdempotencyAdmissionTombstone>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdmitAsync_OneTickAfterExpiry_CompactsAndReturnsExpired()
    {
        DateTimeOffset afterExpiry = _now.AddTicks(1);
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor(afterExpiry);
        IdempotencyAdmissionRecord existing = Record(
            state: IdempotencyAdmissionState.Terminal,
            firstConsumedAt: _now.AddHours(-1),
            lastObservedAt: _now.AddTicks(-1),
            replayExpiresAt: _now,
            replayResult: new CommandProcessingResult(true));
        IdempotencyAdmissionTombstone? compacted = null;
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, existing));
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.TombstoneStateName,
                Arg.Do<IdempotencyAdmissionTombstone>(value => compacted = value),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        IdempotencyAdmissionResult result = await actor.AdmitAsync(Request(intentDigest: "different"));

        result.Decision.ShouldBe(IdempotencyAdmissionDecision.Expired);
        compacted.ShouldNotBeNull().LastObservedAt.ShouldBe(afterExpiry);
    }

    [Fact]
    public async Task AdmitAsync_ExistingTombstone_EquivalentAndDifferentIntentAreIndistinguishable()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        var tombstone = new IdempotencyAdmissionTombstone(
            IdempotencyAdmissionTombstone.CurrentSchemaVersion,
            IdempotencyAdmissionState.Expired,
            "tenant-a",
            "key-digest",
            "verification-tag",
            "v1",
            IdempotencyReplayRetentionTier.Mutation,
            _now.AddDays(-2),
            _now.AddDays(-1),
            _now);
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionTombstone>(
                IdempotencyAdmissionActor.TombstoneStateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionTombstone>(true, tombstone));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(false, default!));

        IdempotencyAdmissionResult equivalent = await actor.AdmitAsync(Request());
        IdempotencyAdmissionResult different = await actor.AdmitAsync(Request(intentDigest: "different"));

        equivalent.ShouldBe(different);
        equivalent.Decision.ShouldBe(IdempotencyAdmissionDecision.Expired);
        equivalent.FencingToken.ShouldBe(0);
        equivalent.ExecutionMessageId.ShouldBeNull();
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdmitAsync_VerificationTagMismatchFailsClosedAsCollision()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        IdempotencyAdmissionRecord existing = Record() with { VerificationTag = "collision-tag" };
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, existing));

        IdempotencyAdmissionResult result = await actor.AdmitAsync(Request());

        result.Decision.ShouldBe(IdempotencyAdmissionDecision.Collision);
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<IdempotencyAdmissionRecord>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Promotion_PreparedTargetCannotAdmitUntilDirectoryFlipActivatesIt()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        var replay = new CommandProcessingResult(true, CorrelationId: "original", ResultPayload: "same");
        IdempotencyAdmissionRecord? storedRecord = null;
        IdempotencyAdmissionPromotionRecord? promotion = null;
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(_ => storedRecord is null
                ? new ConditionalValue<IdempotencyAdmissionRecord>(false, default!)
                : new ConditionalValue<IdempotencyAdmissionRecord>(true, storedRecord));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionPromotionRecord>(
                IdempotencyAdmissionActor.PromotionStateName,
                Arg.Any<CancellationToken>())
            .Returns(_ => promotion is null
                ? new ConditionalValue<IdempotencyAdmissionPromotionRecord>(false, default!)
                : new ConditionalValue<IdempotencyAdmissionPromotionRecord>(true, promotion));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRedirectRecord>(
                IdempotencyAdmissionActor.RedirectStateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRedirectRecord>(false, default!));
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.StateName,
                Arg.Do<IdempotencyAdmissionRecord>(record => storedRecord = record),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.PromotionStateName,
                Arg.Do<IdempotencyAdmissionPromotionRecord>(record => promotion = record),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        IdempotencyAdmissionRecord imported = Record(
            state: IdempotencyAdmissionState.Terminal,
            replayResult: replay);

        await actor.PreparePromotionAsync(
            new IdempotencyAdmissionPromotionImportRequest("tenant-a:v1:source", imported));
        IdempotencyAdmissionResult beforeFlip = await actor.AdmitAsync(Request());
        await actor.ActivatePromotionAsync(
            new IdempotencyAdmissionPromotionActivationRequest("tenant-a:v1:source"));
        IdempotencyAdmissionResult afterFlip = await actor.AdmitAsync(Request());

        beforeFlip.Decision.ShouldBe(IdempotencyAdmissionDecision.Pending);
        afterFlip.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
        afterFlip.ReplayResult.ShouldBe(replay);
        promotion.ShouldNotBeNull().Activated.ShouldBeTrue();
    }

    [Fact]
    public async Task Promotion_SourceRedirectNeverExecutesAndReturnsOnlyProtectedTarget()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        IdempotencyAdmissionRedirectRecord? redirect = null;
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, Record()));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRedirectRecord>(
                IdempotencyAdmissionActor.RedirectStateName,
                Arg.Any<CancellationToken>())
            .Returns(_ => redirect is null
                ? new ConditionalValue<IdempotencyAdmissionRedirectRecord>(false, default!)
                : new ConditionalValue<IdempotencyAdmissionRedirectRecord>(true, redirect));
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.RedirectStateName,
                Arg.Do<IdempotencyAdmissionRedirectRecord>(record => redirect = record),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await actor.SetRedirectAsync(new IdempotencyAdmissionRedirectRequest("tenant-a:v2:target"));
        IdempotencyAdmissionResult result = await actor.AdmitAsync(Request());

        result.Decision.ShouldBe(IdempotencyAdmissionDecision.Redirect);
        result.RedirectActorId.ShouldBe("tenant-a:v2:target");
        result.FencingToken.ShouldBe(0);
    }

    [Fact]
    public async Task AdmitAsync_StateStoreUnavailableFailsClosedWithoutReservation()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns<Task<ConditionalValue<IdempotencyAdmissionRecord>>>(_ =>
                throw new InvalidOperationException("state store unavailable"));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.AdmitAsync(Request()));

        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<IdempotencyAdmissionRecord>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdmitAsync_UnknownSchema_FailsClosedAsCorrupt()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        IdempotencyAdmissionRecord existing = Record() with { SchemaVersion = 99 };
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, existing));

        IdempotencyAdmissionResult result = await actor.AdmitAsync(Request());

        result.Decision.ShouldBe(IdempotencyAdmissionDecision.Corrupt);
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<IdempotencyAdmissionRecord>(),
            Arg.Any<CancellationToken>());
    }

    private static (IdempotencyAdmissionActor Actor, IActorStateManager StateManager, FakeTimeProvider TimeProvider) CreateActor(
        DateTimeOffset? now = null)
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<IdempotencyAdmissionActor> logger = Substitute.For<ILogger<IdempotencyAdmissionActor>>();
        var timeProvider = new FakeTimeProvider(now ?? _now);
        ActorHost host = ActorHost.CreateForTest<IdempotencyAdmissionActor>(
            new ActorTestOptions { ActorId = new ActorId("tenant-a:v1:key-digest") });
        var actor = new IdempotencyAdmissionActor(host, logger, timeProvider);
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        return (actor, stateManager, timeProvider);
    }

    private static IdempotencyKeyProtector CreateProtector()
    {
        return new IdempotencyKeyProtector(
            new StaticIdempotencyDigestKeyProvider(
                "v1",
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    ["v1"] = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"),
                },
                []));
    }

    private static IdempotencyKeyProtector CreateRotatingProtector()
        => new(
            new StaticIdempotencyDigestKeyProvider(
                "v2",
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    ["v1"] = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"),
                    ["v2"] = Encoding.UTF8.GetBytes("abcdef0123456789abcdef0123456789"),
                },
                ["v1"]));

    private static IdempotencyExecutionContextProtector CreateExecutionContextProtector()
        => new(
            new StaticIdempotencyDigestKeyProvider(
                "v1",
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    ["v1"] = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"),
                },
                []));

    private static TrustedIdempotencyDescriptor Descriptor(string target)
        => new(
            "folders",
            "CreateFolder",
            1,
            Encoding.UTF8.GetBytes($"operation\0CreateFolder\0target\0{target}"),
            IdempotencyReplayRetentionTier.Mutation);

    private static IdempotencyAdmissionRequest Request(
        string intentDigest = "intent-a",
        string executionMessageId = "01J00000000000000000000000",
        string executionCorrelationId = "trace-original")
        => new(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            "tenant-a",
            "v1",
            "key-digest",
            "verification-tag",
            intentDigest,
            IdempotencyReplayRetentionTier.Mutation,
            executionMessageId,
            executionCorrelationId);

    private static IdempotencyAdmissionRecord Record(
        IdempotencyAdmissionState state = IdempotencyAdmissionState.Reserved,
        string? intentDigest = "intent-a",
        IdempotencyReplayRetentionTier tier = IdempotencyReplayRetentionTier.Mutation,
        DateTimeOffset? firstConsumedAt = null,
        DateTimeOffset? lastObservedAt = null,
        DateTimeOffset? replayExpiresAt = null,
        CommandProcessingResult? replayResult = null)
        => new(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            state,
            "tenant-a",
            "v1",
            "key-digest",
            "verification-tag",
            intentDigest,
            tier,
            firstConsumedAt ?? _now.AddHours(-1),
            lastObservedAt ?? _now,
            replayExpiresAt ?? (state is IdempotencyAdmissionState.Terminal or IdempotencyAdmissionState.Expired
                ? _now.AddHours(1)
                : null),
            7,
            replayResult,
            "01J00000000000000000000000",
            "trace-original");
}
