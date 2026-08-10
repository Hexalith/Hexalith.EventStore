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
    [Theory]
    [InlineData(IdempotencyAdmissionState.Reserved, IdempotencyAdmissionState.Pending, true)]
    [InlineData(IdempotencyAdmissionState.Reserved, IdempotencyAdmissionState.Recoverable, true)]
    [InlineData(IdempotencyAdmissionState.Pending, IdempotencyAdmissionState.UnknownProviderOutcome, true)]
    [InlineData(IdempotencyAdmissionState.Recoverable, IdempotencyAdmissionState.Pending, true)]
    [InlineData(IdempotencyAdmissionState.UnknownProviderOutcome, IdempotencyAdmissionState.Terminal, true)]
    [InlineData(IdempotencyAdmissionState.Terminal, IdempotencyAdmissionState.Pending, false)]
    [InlineData(IdempotencyAdmissionState.Expired, IdempotencyAdmissionState.Terminal, false)]
    public void StateTransitions_AllowOnlyApprovedEdges(
        IdempotencyAdmissionState from,
        IdempotencyAdmissionState to,
        bool expected)
        => IdempotencyAdmissionStateTransitions.IsAllowed(from, to).ShouldBe(expected);

    [Fact]
    public async Task BeginAsync_StructurallyCorruptRecord_FailsClosedBeforeMutation()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        IdempotencyAdmissionRecord corrupt = Record(state: IdempotencyAdmissionState.Pending) with
        {
            ExecutionMessageId = null,
        };
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, corrupt));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => actor.BeginAsync(new IdempotencyAdmissionTransitionRequest(corrupt.FencingToken)));

        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<IdempotencyAdmissionRecord>(),
            Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

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
        _ = lifecycle.AdmitAsync(Arg.Any<IdempotencyTenantLifecycleAdmissionRequest>())
            .Returns(callInfo =>
            {
                capturedRequest = callInfo.ArgAt<IdempotencyTenantLifecycleAdmissionRequest>(0).Admission;
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
        _ = await lifecycle.Received(1).AdmitAsync(
            Arg.Is<IdempotencyTenantLifecycleAdmissionRequest>(request =>
                !request.Admission.KeyDigest.Contains("opaque-secret-key", StringComparison.Ordinal)
                && !request.Admission.VerificationTag.Contains("opaque-secret-key", StringComparison.Ordinal)
                && !request.Admission.IntentDigest.Contains("target-a", StringComparison.Ordinal)
                && request.Reference.ActorId == session.ActorId));
        await lifecycle.Received(1).RegisterAsync(
            Arg.Is<IdempotencyTenantLifecycleReference[]>(references =>
                references.Length == 1
                && !references[0].ActorId.Contains("opaque-secret-key", StringComparison.Ordinal)
                && !references[0].KeyDigest.Contains("opaque-secret-key", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Coordinator_ValidateExecutionAsync_RequiresDurablePendingAuthority()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor authority = Substitute.For<IIdempotencyAdmissionActor>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(authority);
        _ = authority.ValidateAuthorityAsync(Arg.Any<IdempotencyAdmissionAuthorityRequest>())
            .Returns(Task.CompletedTask);
        IdempotencyExecutionContextProtector protector = new(
            new StaticIdempotencyDigestKeyProvider(
                "v1",
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    ["v1"] = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"),
                },
                []),
            factory);
        var command = new Hexalith.EventStore.Server.Pipeline.Commands.SubmitCommand(
            "01J00000000000000000000000",
            "tenant-a",
            "folders",
            "folder-a",
            "CreateFolderCommand",
            [1],
            "trace-original",
            "user-a");
        IdempotencyExecutionContext context = await protector.ProtectAsync(
            "tenant-a:v1:key-digest",
            7,
            "v1",
            command);
        var session = new IdempotencyAdmissionSession(
            context.AdmissionActorId,
            context.FencingToken,
            IdempotencyAdmissionDecision.Execute,
            ExecutionContext: context,
            ExecutionMessageId: context.MessageId,
            ExecutionCorrelationId: context.CorrelationId);
        IIdempotencyIntentAdapterRegistry registry = Substitute.For<IIdempotencyIntentAdapterRegistry>();
        var coordinator = new IdempotencyAdmissionCoordinator(
            factory,
            CreateProtector(),
            registry,
            protector);

        await coordinator.ValidateExecutionAsync(session, command);

        await authority.Received(1).ValidateAuthorityAsync(
            Arg.Is<IdempotencyAdmissionAuthorityRequest>(request =>
                request.FencingToken == 7
                && request.ExecutionMessageId == command.MessageId
                && request.ExecutionCorrelationId == command.CorrelationId
                && request.Purpose == IdempotencyExecutionPurpose.Execute));
    }

    [Fact]
    public async Task Coordinator_ValidateExecutionCapabilityAsync_TamperedProofFailsBeforeActorAccess()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor authority = Substitute.For<IIdempotencyAdmissionActor>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(authority);
        var protector = new IdempotencyExecutionContextProtector(
            new StaticIdempotencyDigestKeyProvider(
                "v1",
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    ["v1"] = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"),
                },
                []),
            factory);
        var command = new Hexalith.EventStore.Server.Pipeline.Commands.SubmitCommand(
            "01J00000000000000000000000",
            "tenant-a",
            "folders",
            "folder-a",
            "CreateFolderCommand",
            [1],
            "trace-original",
            "user-a");
        IdempotencyExecutionContext signed = await protector.ProtectAsync(
            "tenant-a:v1:key-digest",
            7,
            "v1",
            command);
        IdempotencyExecutionContext tampered = signed with { FencingToken = 8 };
        var session = new IdempotencyAdmissionSession(
            tampered.AdmissionActorId,
            tampered.FencingToken,
            IdempotencyAdmissionDecision.Execute,
            ExecutionContext: tampered,
            ExecutionMessageId: tampered.MessageId,
            ExecutionCorrelationId: tampered.CorrelationId);
        var coordinator = new IdempotencyAdmissionCoordinator(
            factory,
            CreateProtector(),
            Substitute.For<IIdempotencyIntentAdapterRegistry>(),
            protector);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => coordinator.ValidateExecutionCapabilityAsync(session, command));

        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
            default!,
            default!);
        await authority.DidNotReceive().ValidateAuthorityAsync(
            Arg.Any<IdempotencyAdmissionAuthorityRequest>());
    }

    [Fact]
    public async Task Coordinator_ValidateExecutionAsync_UnknownOutcomeRequiresExactReconciliationAuthority()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor authority = Substitute.For<IIdempotencyAdmissionActor>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(authority);
        var keyProvider = new StaticIdempotencyDigestKeyProvider(
            "v1",
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["v1"] = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"),
            },
            []);
        var protector = new IdempotencyExecutionContextProtector(keyProvider, factory);
        var command = new Hexalith.EventStore.Server.Pipeline.Commands.SubmitCommand(
            "01J00000000000000000000000",
            "tenant-a",
            "folders",
            "folder-a",
            "CreateFolderCommand",
            [1],
            "trace-original",
            "user-a");
        IdempotencyExecutionContext context = await protector.ProtectAsync(
            "tenant-a:v1:key-digest",
            7,
            "v1",
            command);
        var session = new IdempotencyAdmissionSession(
            context.AdmissionActorId,
            context.FencingToken,
            IdempotencyAdmissionDecision.UnknownProviderOutcome,
            ExecutionContext: context,
            ExecutionMessageId: context.MessageId,
            ExecutionCorrelationId: context.CorrelationId);
        var coordinator = new IdempotencyAdmissionCoordinator(
            factory,
            CreateProtector(),
            Substitute.For<IIdempotencyIntentAdapterRegistry>(),
            protector);

        await coordinator.ValidateExecutionAsync(session, command);

        await authority.Received(1).ValidateAuthorityAsync(
            Arg.Is<IdempotencyAdmissionAuthorityRequest>(request =>
                request.FencingToken == context.FencingToken
                && request.DigestKeyVersion == context.DigestKeyVersion
                && request.ExecutionMessageId == context.MessageId
                && request.ExecutionCorrelationId == context.CorrelationId
                && request.Purpose == IdempotencyExecutionPurpose.Reconcile));
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
    public async Task Coordinator_PostDeletionLifecycleRejectsBeforeAdmissionActorAccess()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyTenantLifecycleActor lifecycle = Substitute.For<IIdempotencyTenantLifecycleActor>();
        IIdempotencyLegacyInventoryActor inventory = Substitute.For<IIdempotencyLegacyInventoryActor>();
        _ = factory.CreateActorProxy<IIdempotencyTenantLifecycleActor>(
                Arg.Any<ActorId>(),
                IdempotencyTenantLifecycleActor.ActorTypeName)
            .Returns(lifecycle);
        _ = factory.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyLegacyInventoryActor.ActorTypeName)
            .Returns(inventory);
        _ = inventory.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>())
            .Returns(new IdempotencyLegacyInventoryInspection(IdempotencyLegacyInventoryDecision.NoLegacy));
        _ = lifecycle.RegisterAsync(Arg.Any<IdempotencyTenantLifecycleReference[]>())
            .Returns<Task>(_ => throw new InvalidOperationException("tenant deletion"));
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
        registry.Resolve(command).Returns(Descriptor("target-a"));
        var coordinator = new IdempotencyAdmissionCoordinator(
            factory,
            CreateProtector(),
            registry,
            CreateExecutionContextProtector());

        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.AdmitAsync(command));

        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
            default!,
            default!);
        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
            default!,
            default!);
    }

    [Fact]
    public async Task Coordinator_DeletionAfterRegistrationRejectsInsideSerializedAdmissionTurn()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor admission = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        IIdempotencyTenantLifecycleActor lifecycle = Substitute.For<IIdempotencyTenantLifecycleActor>();
        IIdempotencyLegacyInventoryActor inventory = Substitute.For<IIdempotencyLegacyInventoryActor>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(admission);
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
        _ = admission.InspectAsync().Returns(new IdempotencyAdmissionInspection(false));
        _ = inventory.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>())
            .Returns(new IdempotencyLegacyInventoryInspection(IdempotencyLegacyInventoryDecision.NoLegacy));
        _ = directory.ResolveAsync(Arg.Any<IdempotencyAdmissionDirectoryRequest>())
            .Returns(call => new IdempotencyAdmissionDirectoryResult(
                call.ArgAt<IdempotencyAdmissionDirectoryRequest>(0).ActiveActorId,
                IdempotencyAdmissionPromotionPhase.Stable));
        _ = lifecycle.AdmitAsync(Arg.Any<IdempotencyTenantLifecycleAdmissionRequest>())
            .Returns<Task<IdempotencyAdmissionResult>>(_ =>
                throw new InvalidOperationException("tenant deletion won serialized admission"));
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
        IIdempotencyIntentAdapterRegistry registry = Substitute.For<IIdempotencyIntentAdapterRegistry>();
        registry.Resolve(command).Returns(Descriptor("target-a"));
        var coordinator = new IdempotencyAdmissionCoordinator(
            factory,
            CreateProtector(),
            registry,
            CreateExecutionContextProtector());

        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.AdmitAsync(command));

        await lifecycle.Received(1).RegisterAsync(Arg.Any<IdempotencyTenantLifecycleReference[]>());
        _ = await lifecycle.Received(1).AdmitAsync(
            Arg.Is<IdempotencyTenantLifecycleAdmissionRequest>(request =>
                request.Reference.ActorId.EndsWith(request.Reference.KeyDigest, StringComparison.Ordinal)
                && request.Admission.KeyDigest == request.Reference.KeyDigest));
        _ = await admission.DidNotReceive().AdmitAsync(Arg.Any<IdempotencyAdmissionRequest>());
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
        IdempotencyAdmissionPromotionAcknowledgement? targetProof = null;
        bool targetActivated = false;
        _ = source.InspectAsync().Returns(new IdempotencyAdmissionInspection(true, retainedRecord));
        _ = target.InspectAsync().Returns(_ => targetProof is null
            ? new IdempotencyAdmissionInspection(false)
            : new IdempotencyAdmissionInspection(
                true,
                Promotion: targetProof with { Activated = targetActivated }));
        _ = lifecycle.AdmitAsync(Arg.Is<IdempotencyTenantLifecycleAdmissionRequest>(request =>
                request.Reference.ActorId == retained.ActorId))
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
        _ = target.AcknowledgePromotionAsync(
                Arg.Any<IdempotencyAdmissionPromotionAcknowledgementRequest>())
            .Returns(callInfo =>
            {
                IdempotencyAdmissionPromotionAcknowledgementRequest request = callInfo
                    .ArgAt<IdempotencyAdmissionPromotionAcknowledgementRequest>(0);
                calls.Add("acknowledge");
                targetProof = new IdempotencyAdmissionPromotionAcknowledgement(
                    IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion,
                    request.SourceActorId,
                    request.MigrationId,
                    request.SourceEvidenceDigest,
                    request.ImportDigest,
                    Activated: targetActivated);
                return targetProof;
            });
        _ = source.SetRedirectAsync(Arg.Any<IdempotencyAdmissionRedirectRequest>())
            .Returns(_ => { calls.Add("redirect"); return Task.CompletedTask; });
        _ = target.ActivatePromotionAsync(Arg.Any<IdempotencyAdmissionPromotionActivationRequest>())
            .Returns(_ =>
            {
                calls.Add("activate");
                targetActivated = true;
                return Task.CompletedTask;
            });
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
        _ = lifecycle.AdmitAsync(Arg.Is<IdempotencyTenantLifecycleAdmissionRequest>(request =>
                request.Reference.ActorId == active.ActorId))
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
            "acknowledge",
            "advance:PrepareTarget",
            "redirect",
            "advance:RedirectSource",
            "advance:FlipDirectory",
            "activate",
            "advance:ActivateTarget",
            "admit",
        ]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Coordinator_OrdinaryActivationResponseLossReprovesExactTargetBeforeAdvance(
        bool corruptProof)
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        IIdempotencyTenantLifecycleActor lifecycle = Substitute.For<IIdempotencyTenantLifecycleActor>();
        IIdempotencyLegacyInventoryActor inventory = Substitute.For<IIdempotencyLegacyInventoryActor>();
        IdempotencyKeyProtector protector = CreateRotatingProtector();
        IdempotencyProtectedIdentitySet identities = await protector.ProtectAsync(
            "tenant-a",
            "opaque-secret-key",
            Descriptor("target-a"));
        IdempotencyProtectedIdentity sourceIdentity = identities.Aliases.Single(identity =>
            identity.DigestKeyVersion == "v1");
        IdempotencyProtectedIdentity targetIdentity = identities.Active;
        IIdempotencyAdmissionActor source = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionActor target = Substitute.For<IIdempotencyAdmissionActor>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(callInfo => string.Equals(
                callInfo.ArgAt<ActorId>(0).GetId(),
                sourceIdentity.ActorId,
                StringComparison.Ordinal)
                    ? source
                    : target);
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
            .Returns(new IdempotencyLegacyInventoryInspection(IdempotencyLegacyInventoryDecision.NoLegacy));
        string migrationId = IdempotencyAdmissionPromotionEvidence.BuildConventionalMigrationId(
            sourceIdentity.ActorId,
            targetIdentity.ActorId);
        var proof = new IdempotencyAdmissionPromotionAcknowledgement(
            IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion,
            sourceIdentity.ActorId,
            corruptProof ? "stale-migration" : migrationId,
            "target-import",
            "target-import",
            Activated: true,
            CurrentStateDigest: "current-target-state");
        _ = source.InspectAsync().Returns(new IdempotencyAdmissionInspection(
            true,
            RedirectActorId: targetIdentity.ActorId));
        _ = target.InspectAsync().Returns(new IdempotencyAdmissionInspection(
            true,
            Promotion: proof));
        _ = directory.ResolveAsync(Arg.Any<IdempotencyAdmissionDirectoryRequest>())
            .Returns(new IdempotencyAdmissionDirectoryResult(
                targetIdentity.ActorId,
                IdempotencyAdmissionPromotionPhase.ActivateTarget,
                sourceIdentity.ActorId,
                targetIdentity.ActorId));
        _ = directory.AdvanceAsync(Arg.Any<IdempotencyAdmissionDirectoryAdvanceRequest>())
            .Returns(new IdempotencyAdmissionDirectoryResult(
                targetIdentity.ActorId,
                IdempotencyAdmissionPromotionPhase.Stable));
        _ = lifecycle.AdmitAsync(Arg.Is<IdempotencyTenantLifecycleAdmissionRequest>(request =>
                request.Reference.ActorId == sourceIdentity.ActorId))
            .Returns(new IdempotencyAdmissionResult(
                IdempotencyAdmissionDecision.Redirect,
                RedirectActorId: targetIdentity.ActorId));
        _ = lifecycle.AdmitAsync(Arg.Is<IdempotencyTenantLifecycleAdmissionRequest>(request =>
                request.Reference.ActorId == targetIdentity.ActorId))
            .Returns(new IdempotencyAdmissionResult(
                IdempotencyAdmissionDecision.Replay,
                1,
                new CommandProcessingResult(true)));
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
        IIdempotencyIntentAdapterRegistry registry = Substitute.For<IIdempotencyIntentAdapterRegistry>();
        registry.Resolve(command).Returns(Descriptor("target-a"));
        var coordinator = new IdempotencyAdmissionCoordinator(factory, protector, registry);

        if (corruptProof)
        {
            _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.AdmitAsync(command));
            await target.DidNotReceive().ActivatePromotionAsync(
                Arg.Any<IdempotencyAdmissionPromotionActivationRequest>());
            _ = await directory.DidNotReceive().AdvanceAsync(
                Arg.Any<IdempotencyAdmissionDirectoryAdvanceRequest>());
            return;
        }

        IdempotencyAdmissionSession result = (await coordinator.AdmitAsync(command)).ShouldNotBeNull();
        result.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
        await target.Received(1).ActivatePromotionAsync(
            Arg.Is<IdempotencyAdmissionPromotionActivationRequest>(request =>
                request.SourceActorId == sourceIdentity.ActorId
                && request.MigrationId == migrationId
                && request.ImportDigest == proof.ImportDigest));
        _ = await directory.Received(1).AdvanceAsync(
            Arg.Is<IdempotencyAdmissionDirectoryAdvanceRequest>(request =>
                request.ExpectedPhase == IdempotencyAdmissionPromotionPhase.ActivateTarget));
    }

    [Theory]
    [InlineData(IdempotencyLegacyInventoryDecision.Uninventoried)]
    [InlineData(IdempotencyLegacyInventoryDecision.Unsafe)]
    public async Task Coordinator_UnsafeLegacyInventoryDoesNoLifecycleAdmissionDirectoryOrMigrationWork(
        IdempotencyLegacyInventoryDecision decision)
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor admission = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        IIdempotencyTenantLifecycleActor lifecycle = Substitute.For<IIdempotencyTenantLifecycleActor>();
        IIdempotencyTenantLifecycleMigrationActor migration = Substitute.For<IIdempotencyTenantLifecycleMigrationActor>();
        IIdempotencyLegacyInventoryActor inventory = Substitute.For<IIdempotencyLegacyInventoryActor>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(admission);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directory);
        _ = factory.CreateActorProxy<IIdempotencyTenantLifecycleActor>(
                Arg.Any<ActorId>(),
                IdempotencyTenantLifecycleActor.ActorTypeName)
            .Returns(lifecycle);
        _ = factory.CreateActorProxy<IIdempotencyTenantLifecycleMigrationActor>(
                Arg.Any<ActorId>(),
                IdempotencyTenantLifecycleActor.ActorTypeName)
            .Returns(migration);
        _ = factory.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyLegacyInventoryActor.ActorTypeName)
            .Returns(inventory);
        _ = inventory.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>())
            .Returns(new IdempotencyLegacyInventoryInspection(decision));
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
        IIdempotencyIntentAdapterRegistry registry = Substitute.For<IIdempotencyIntentAdapterRegistry>();
        registry.Resolve(command).Returns(Descriptor("target-a"));
        var coordinator = new IdempotencyAdmissionCoordinator(
            factory,
            CreateProtector(),
            registry,
            CreateExecutionContextProtector());

        IdempotencyAdmissionSession session = (await coordinator.AdmitAsync(command)).ShouldNotBeNull();

        session.Decision.ShouldBe(IdempotencyAdmissionDecision.UnsafeLegacy);
        _ = await inventory.Received(1).InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>());
        await lifecycle.DidNotReceive().RegisterAsync(Arg.Any<IdempotencyTenantLifecycleReference[]>());
        _ = await lifecycle.DidNotReceive().AdmitAsync(Arg.Any<IdempotencyTenantLifecycleAdmissionRequest>());
        _ = await admission.DidNotReceive().InspectAsync();
        _ = await admission.DidNotReceive().AdmitAsync(Arg.Any<IdempotencyAdmissionRequest>());
        await admission.DidNotReceive().PreparePromotionAsync(
            Arg.Any<IdempotencyAdmissionPromotionImportRequest>());
        _ = await admission.DidNotReceive().AcknowledgePromotionAsync(
            Arg.Any<IdempotencyAdmissionPromotionAcknowledgementRequest>());
        await admission.DidNotReceive().SetRedirectAsync(Arg.Any<IdempotencyAdmissionRedirectRequest>());
        await admission.DidNotReceive().ActivatePromotionAsync(
            Arg.Any<IdempotencyAdmissionPromotionActivationRequest>());
        await admission.DidNotReceive().ValidateAuthorityAsync(
            Arg.Any<IdempotencyAdmissionAuthorityRequest>());
        _ = await directory.DidNotReceive().ResolveAsync(Arg.Any<IdempotencyAdmissionDirectoryRequest>());
        _ = await directory.DidNotReceive().AdvanceAsync(Arg.Any<IdempotencyAdmissionDirectoryAdvanceRequest>());
        _ = await migration.DidNotReceive().MigrateLegacyAsync(Arg.Any<IdempotencyLegacyMigrationRequest>());
    }

    [Fact]
    public async Task Coordinator_ExactLegacyInventory_PreparesActivatesAndRedirectsBeforeReplay()
    {
        var calls = new List<string>();
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor target = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        IIdempotencyTenantLifecycleActor lifecycle = Substitute.For<IIdempotencyTenantLifecycleActor>();
        IIdempotencyTenantLifecycleMigrationActor migration = Substitute.For<IIdempotencyTenantLifecycleMigrationActor>();
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
            IdempotencyLegacyMigrationPhase.Inventoried,
            "inventory-2026-08",
            1,
            "migration-01J00000000000000000000000");
        IdempotencyLegacyMigrationRequest? migrationRequest = null;
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
        _ = factory.CreateActorProxy<IIdempotencyTenantLifecycleMigrationActor>(
                Arg.Any<ActorId>(),
                IdempotencyTenantLifecycleActor.ActorTypeName)
            .Returns(migration);
        _ = factory.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyLegacyInventoryActor.ActorTypeName)
            .Returns(inventory);
        _ = inventory.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>())
            .Returns(new IdempotencyLegacyInventoryInspection(
                IdempotencyLegacyInventoryDecision.Migrate,
                entry));
        _ = migration.MigrateLegacyAsync(Arg.Any<IdempotencyLegacyMigrationRequest>())
            .Returns(callInfo =>
            {
                migrationRequest = callInfo.ArgAt<IdempotencyLegacyMigrationRequest>(0);
                calls.Add("migrate");
                return new IdempotencyLegacyMigrationResult(identity.ActorId);
            });
        _ = target.InspectAsync().Returns(new IdempotencyAdmissionInspection(false));
        _ = directory.ResolveAsync(Arg.Any<IdempotencyAdmissionDirectoryRequest>())
            .Returns(new IdempotencyAdmissionDirectoryResult(
                identity.ActorId,
                IdempotencyAdmissionPromotionPhase.Stable));
        _ = lifecycle.AdmitAsync(Arg.Any<IdempotencyTenantLifecycleAdmissionRequest>())
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
        migrationRequest.ShouldNotBeNull().Target.ActorId.ShouldBe(identity.ActorId);
        migrationRequest.TargetIntentDigest.ShouldBe(identity.IntentDigest);
        calls.ShouldBe(["migrate"]);
    }

    [Theory]
    [InlineData(IdempotencyAdmissionDecision.UnsafeLegacy)]
    [InlineData(IdempotencyAdmissionDecision.Conflict)]
    public async Task Coordinator_MigratedFailedReproofStopsBeforeDirectoryOrAdmission(
        IdempotencyAdmissionDecision denied)
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor admission = Substitute.For<IIdempotencyAdmissionActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        IIdempotencyTenantLifecycleActor lifecycle = Substitute.For<IIdempotencyTenantLifecycleActor>();
        IIdempotencyTenantLifecycleMigrationActor migration = Substitute.For<IIdempotencyTenantLifecycleMigrationActor>();
        IIdempotencyLegacyInventoryActor inventory = Substitute.For<IIdempotencyLegacyInventoryActor>();
        IdempotencyKeyProtector protector = CreateProtector();
        IdempotencyProtectedIdentity identity = (await protector.ProtectAsync(
            "tenant-a",
            "opaque-secret-key",
            Descriptor("target-a"))).Active;
        var entry = new IdempotencyLegacyInventoryEntry(
            IdempotencyLegacyInventoryEntry.CurrentSchemaVersion,
            "tenant-a",
            "tenant-a:folders:legacy-folder",
            "source-evidence",
            1,
            identity.DigestKeyVersion,
            identity.KeyDigest,
            identity.VerificationTag,
            identity.IntentDigest,
            identity.RetentionTier,
            _now.AddDays(-1),
            _now,
            _now.AddDays(1),
            new CommandProcessingResult(true),
            "01J00000000000000000000000",
            "trace-original",
            IdempotencyLegacyMigrationPhase.Migrated,
            "inventory-2026-08",
            1,
            "migration-01J00000000000000000000000",
            identity.ActorId,
            "target-import",
            "source-redirect");
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(admission);
        _ = factory.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionDirectoryActor.ActorTypeName)
            .Returns(directory);
        _ = factory.CreateActorProxy<IIdempotencyTenantLifecycleActor>(
                Arg.Any<ActorId>(),
                IdempotencyTenantLifecycleActor.ActorTypeName)
            .Returns(lifecycle);
        _ = factory.CreateActorProxy<IIdempotencyTenantLifecycleMigrationActor>(
                Arg.Any<ActorId>(),
                IdempotencyTenantLifecycleActor.ActorTypeName)
            .Returns(migration);
        _ = factory.CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                Arg.Any<ActorId>(),
                IdempotencyLegacyInventoryActor.ActorTypeName)
            .Returns(inventory);
        _ = inventory.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>())
            .Returns(new IdempotencyLegacyInventoryInspection(
                IdempotencyLegacyInventoryDecision.Migrated,
                entry));
        _ = migration.MigrateLegacyAsync(Arg.Any<IdempotencyLegacyMigrationRequest>())
            .Returns(new IdempotencyLegacyMigrationResult(identity.ActorId, denied));
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
        registry.Resolve(command).Returns(Descriptor("target-a"));
        var coordinator = new IdempotencyAdmissionCoordinator(factory, protector, registry);

        IdempotencyAdmissionSession result = (await coordinator.AdmitAsync(command)).ShouldNotBeNull();

        result.Decision.ShouldBe(denied);
        _ = await directory.DidNotReceive().ResolveAsync(Arg.Any<IdempotencyAdmissionDirectoryRequest>());
        _ = await admission.DidNotReceive().InspectAsync();
        _ = await lifecycle.DidNotReceive().AdmitAsync(Arg.Any<IdempotencyTenantLifecycleAdmissionRequest>());
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
    public async Task MarkRecoveryAsync_PendingTransitionsDirectlyToRecoverableUnderSameFence()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        IdempotencyAdmissionRecord pending = Record(state: IdempotencyAdmissionState.Pending);
        IdempotencyAdmissionRecord? recoverable = null;
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, pending));
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.StateName,
                Arg.Do<IdempotencyAdmissionRecord>(record => recoverable = record),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await actor.MarkRecoveryAsync(
            new IdempotencyAdmissionRecoveryRequest(
                pending.FencingToken,
                IdempotencyAdmissionState.Recoverable));

        recoverable.ShouldNotBeNull().State.ShouldBe(IdempotencyAdmissionState.Recoverable);
        recoverable.FencingToken.ShouldBe(pending.FencingToken);
        recoverable.ExecutionMessageId.ShouldBe(pending.ExecutionMessageId);
        recoverable.ExecutionCorrelationId.ShouldBe(pending.ExecutionCorrelationId);
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
    public async Task Promotion_HashBoundAcknowledgementAllowsOnlyExactPreRedirectRollback()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        IdempotencyAdmissionTombstone? storedTombstone = null;
        IdempotencyAdmissionPromotionRecord? promotion = null;
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionTombstone>(
                IdempotencyAdmissionActor.TombstoneStateName,
                Arg.Any<CancellationToken>())
            .Returns(_ => storedTombstone is null
                ? new ConditionalValue<IdempotencyAdmissionTombstone>(false, default!)
                : new ConditionalValue<IdempotencyAdmissionTombstone>(true, storedTombstone));
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
                IdempotencyAdmissionActor.TombstoneStateName,
                Arg.Do<IdempotencyAdmissionTombstone>(value => storedTombstone = value),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.PromotionStateName,
                Arg.Do<IdempotencyAdmissionPromotionRecord>(value => promotion = value),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
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
        string importDigest = IdempotencyAdmissionPromotionEvidence.Compute(null, tombstone);
        var acknowledgementRequest = new IdempotencyAdmissionPromotionAcknowledgementRequest(
            "tenant-a:folders:legacy-folder",
            "migration-01J00000000000000000000000",
            "source-evidence",
            importDigest);

        await actor.PreparePromotionAsync(
            new IdempotencyAdmissionPromotionImportRequest(
                acknowledgementRequest.SourceActorId,
                Tombstone: tombstone,
                MigrationId: acknowledgementRequest.MigrationId,
                SourceEvidenceDigest: acknowledgementRequest.SourceEvidenceDigest));
        IdempotencyAdmissionPromotionAcknowledgement acknowledgement = await actor
            .AcknowledgePromotionAsync(acknowledgementRequest);
        await actor.RollbackPromotionAsync(
            new IdempotencyAdmissionPromotionRollbackRequest(
                acknowledgement.SourceActorId,
                acknowledgement.MigrationId,
                acknowledgement.SourceEvidenceDigest,
                acknowledgement.ImportDigest));

        acknowledgement.Activated.ShouldBeFalse();
        await stateManager.Received(1).TryRemoveStateAsync(
            IdempotencyAdmissionActor.TombstoneStateName,
            Arg.Any<CancellationToken>());
        await stateManager.Received(1).TryRemoveStateAsync(
            IdempotencyAdmissionActor.PromotionStateName,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Promotion_CompactionPreservesOriginalImportProofAndMovesCurrentCheckpoint()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, FakeTimeProvider time) = CreateActor();
        IdempotencyAdmissionRecord importedRecord = Record(
            IdempotencyAdmissionState.Terminal,
            replayExpiresAt: _now.AddMinutes(1),
            replayResult: new CommandProcessingResult(true, ResultPayload: "replay"));
        IdempotencyAdmissionRecord? record = null;
        IdempotencyAdmissionTombstone? tombstone = null;
        IdempotencyAdmissionPromotionRecord? promotion = null;
        IdempotencyAdmissionRedirectRecord? redirect = null;
        ConfigurePromotionState(
            stateManager,
            () => record,
            value => record = value,
            () => tombstone,
            value => tombstone = value,
            () => promotion,
            value => promotion = value,
            () => redirect);
        const string SourceActorId = "tenant-a:v0:source";
        string migrationId = IdempotencyAdmissionPromotionEvidence.BuildConventionalMigrationId(
            SourceActorId,
            "tenant-a:v1:key-digest");
        string importDigest = IdempotencyAdmissionPromotionEvidence.Compute(importedRecord, null);
        var request = new IdempotencyAdmissionPromotionAcknowledgementRequest(
            SourceActorId,
            migrationId,
            importDigest,
            importDigest);
        await actor.PreparePromotionAsync(new IdempotencyAdmissionPromotionImportRequest(
            SourceActorId,
            importedRecord,
            MigrationId: migrationId,
            SourceEvidenceDigest: importDigest));
        time.Advance(TimeSpan.FromMinutes(2));

        await actor.ReceiveReminderAsync(
            IdempotencyAdmissionActor.CompactionReminderName,
            [],
            TimeSpan.Zero,
            TimeSpan.FromHours(1));
        await actor.PreparePromotionAsync(new IdempotencyAdmissionPromotionImportRequest(
            SourceActorId,
            importedRecord,
            MigrationId: migrationId,
            SourceEvidenceDigest: importDigest));
        IdempotencyAdmissionPromotionAcknowledgement compacted = await actor
            .AcknowledgePromotionAsync(request);

        compacted.ImportDigest.ShouldBe(importDigest);
        compacted.CurrentStateDigest.ShouldBe(
            IdempotencyAdmissionPromotionEvidence.Compute(null, tombstone));
        compacted.CurrentStateDigest.ShouldNotBe(importDigest);
        await actor.RollbackPromotionAsync(new IdempotencyAdmissionPromotionRollbackRequest(
            request.SourceActorId,
            request.MigrationId,
            request.SourceEvidenceDigest,
            request.ImportDigest));

        await actor.PreparePromotionAsync(new IdempotencyAdmissionPromotionImportRequest(
            SourceActorId,
            importedRecord,
            MigrationId: migrationId,
            SourceEvidenceDigest: importDigest));
        await actor.ActivatePromotionAsync(new IdempotencyAdmissionPromotionActivationRequest(
            SourceActorId,
            migrationId,
            importDigest));
        await actor.ReceiveReminderAsync(
            IdempotencyAdmissionActor.CompactionReminderName,
            [],
            TimeSpan.Zero,
            TimeSpan.FromHours(1));
        IdempotencyAdmissionPromotionAcknowledgement activatedCompacted = await actor
            .AcknowledgePromotionAsync(request);

        activatedCompacted.Activated.ShouldBeTrue();
        activatedCompacted.ImportDigest.ShouldBe(importDigest);
        activatedCompacted.CurrentStateDigest.ShouldBe(
            IdempotencyAdmissionPromotionEvidence.Compute(null, tombstone));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Promotion_SchemaOneOrdinaryMarkerNormalizesOnReadAcrossUpgrade(bool activated)
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        IdempotencyAdmissionRecord record = Record();
        IdempotencyAdmissionPromotionRecord marker = new(1, "tenant-a:v0:source", activated);
        IdempotencyAdmissionPromotionRecord? persisted = marker;
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, record));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionTombstone>(
                IdempotencyAdmissionActor.TombstoneStateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionTombstone>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRedirectRecord>(
                IdempotencyAdmissionActor.RedirectStateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRedirectRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionPromotionRecord>(
                IdempotencyAdmissionActor.PromotionStateName,
                Arg.Any<CancellationToken>())
            .Returns(_ => new ConditionalValue<IdempotencyAdmissionPromotionRecord>(true, persisted!));
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.PromotionStateName,
                Arg.Do<IdempotencyAdmissionPromotionRecord>(value => persisted = value),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        IdempotencyAdmissionInspection inspection = await actor.InspectAsync();

        inspection.Promotion.ShouldNotBeNull().SchemaVersion.ShouldBe(
            IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion);
        inspection.Promotion.Activated.ShouldBe(activated);
        inspection.Promotion.MigrationId.ShouldBe(
            IdempotencyAdmissionPromotionEvidence.BuildConventionalMigrationId(
                marker.SourceActorId,
                "tenant-a:v1:key-digest"));
        inspection.Promotion.ImportDigest.ShouldBe(
            IdempotencyAdmissionPromotionEvidence.Compute(record, null));
    }

    [Theory]
    [InlineData("activated")]
    [InlineData("redirected")]
    [InlineData("binding")]
    [InlineData("current")]
    public async Task Promotion_RollbackRejectsEveryUnsafeTargetWithoutRemovalOrSave(string failure)
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        IdempotencyAdmissionRecord record = Record();
        string digest = IdempotencyAdmissionPromotionEvidence.Compute(record, null);
        const string SourceActorId = "tenant-a:v0:source";
        string migrationId = IdempotencyAdmissionPromotionEvidence.BuildConventionalMigrationId(
            SourceActorId,
            "tenant-a:v1:key-digest");
        var promotion = new IdempotencyAdmissionPromotionRecord(
            IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion,
            SourceActorId,
            failure == "activated",
            migrationId,
            digest,
            digest,
            failure == "current" ? "tampered-current" : digest);
        IdempotencyAdmissionRedirectRecord? redirect = failure == "redirected"
            ? new IdempotencyAdmissionRedirectRecord(
                IdempotencyAdmissionRedirectRecord.CurrentSchemaVersion,
                "tenant-a:v2:target")
            : null;
        ConfigurePromotionState(
            stateManager,
            () => record,
            _ => { },
            () => null,
            _ => { },
            () => promotion,
            _ => { },
            () => redirect);
        stateManager.ClearReceivedCalls();

        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.RollbackPromotionAsync(
            new IdempotencyAdmissionPromotionRollbackRequest(
                SourceActorId,
                migrationId,
                digest,
                failure == "binding" ? "different-import" : digest)));

        _ = await stateManager.DidNotReceive().TryRemoveStateAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
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
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var timeProvider = new FakeTimeProvider(now ?? _now);
        ActorHost host = ActorHost.CreateForTest<IdempotencyAdmissionActor>(
            new ActorTestOptions
            {
                ActorId = new ActorId("tenant-a:v1:key-digest"),
                TimerManager = timerManager,
            });
        var actor = new IdempotencyAdmissionActor(host, logger, timeProvider);
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        return (actor, stateManager, timeProvider);
    }

    private static void ConfigurePromotionState(
        IActorStateManager stateManager,
        Func<IdempotencyAdmissionRecord?> getRecord,
        Action<IdempotencyAdmissionRecord?> setRecord,
        Func<IdempotencyAdmissionTombstone?> getTombstone,
        Action<IdempotencyAdmissionTombstone?> setTombstone,
        Func<IdempotencyAdmissionPromotionRecord?> getPromotion,
        Action<IdempotencyAdmissionPromotionRecord?> setPromotion,
        Func<IdempotencyAdmissionRedirectRecord?> getRedirect)
    {
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(_ => getRecord() is { } value
                ? new ConditionalValue<IdempotencyAdmissionRecord>(true, value)
                : new ConditionalValue<IdempotencyAdmissionRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionTombstone>(
                IdempotencyAdmissionActor.TombstoneStateName,
                Arg.Any<CancellationToken>())
            .Returns(_ => getTombstone() is { } value
                ? new ConditionalValue<IdempotencyAdmissionTombstone>(true, value)
                : new ConditionalValue<IdempotencyAdmissionTombstone>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionPromotionRecord>(
                IdempotencyAdmissionActor.PromotionStateName,
                Arg.Any<CancellationToken>())
            .Returns(_ => getPromotion() is { } value
                ? new ConditionalValue<IdempotencyAdmissionPromotionRecord>(true, value)
                : new ConditionalValue<IdempotencyAdmissionPromotionRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRedirectRecord>(
                IdempotencyAdmissionActor.RedirectStateName,
                Arg.Any<CancellationToken>())
            .Returns(_ => getRedirect() is { } value
                ? new ConditionalValue<IdempotencyAdmissionRedirectRecord>(true, value)
                : new ConditionalValue<IdempotencyAdmissionRedirectRecord>(false, default!));
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.StateName,
                Arg.Do<IdempotencyAdmissionRecord>(value => setRecord(value)),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.TombstoneStateName,
                Arg.Do<IdempotencyAdmissionTombstone>(value => setTombstone(value)),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.PromotionStateName,
                Arg.Do<IdempotencyAdmissionPromotionRecord>(value => setPromotion(value)),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _ = stateManager.TryRemoveStateAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                switch (callInfo.ArgAt<string>(0))
                {
                    case IdempotencyAdmissionActor.StateName:
                        setRecord(null);
                        break;
                    case IdempotencyAdmissionActor.TombstoneStateName:
                        setTombstone(null);
                        break;
                    case IdempotencyAdmissionActor.PromotionStateName:
                        setPromotion(null);
                        break;
                }

                return true;
            });
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
