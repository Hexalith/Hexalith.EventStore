using System.Text;

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
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(proxy);
        _ = proxy.AdmitAsync(Arg.Any<IdempotencyAdmissionRequest>())
            .Returns(new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Execute, 1));
        var coordinator = new IdempotencyAdmissionCoordinator(factory, CreateProtector());
        var command = new Hexalith.EventStore.Server.Pipeline.Commands.SubmitCommand(
            MessageId: "opaque-secret-key",
            Tenant: "tenant-a",
            Domain: "folders",
            AggregateId: "folder-a",
            CommandType: "CreateFolderCommand",
            Payload: [1],
            CorrelationId: "trace-a",
            UserId: "user-a",
            Idempotency: Descriptor("target-a"));

        IdempotencyAdmissionSession session = (await coordinator.AdmitAsync(command)).ShouldNotBeNull();

        session.ActorId.ShouldNotContain("opaque-secret-key");
        session.ExecutionMessageId.ShouldNotBeNullOrWhiteSpace();
        session.ExecutionMessageId.ShouldNotBe(session.ActorId);
        session.ExecutionMessageId.ShouldNotContain("opaque-secret-key");
        _ = factory.Received(1).CreateActorProxy<IIdempotencyAdmissionActor>(
            Arg.Is<ActorId>(actorId => !actorId.ToString().Contains("opaque-secret-key", StringComparison.Ordinal)),
            IdempotencyAdmissionActor.ActorTypeName);
        await proxy.Received(1).AdmitAsync(
            Arg.Is<IdempotencyAdmissionRequest>(request =>
                !request.KeyDigest.Contains("opaque-secret-key", StringComparison.Ordinal)
                && !request.VerificationTag.Contains("opaque-secret-key", StringComparison.Ordinal)
                && !request.IntentDigest.Contains("target-a", StringComparison.Ordinal)));
    }

    [Fact]
    public void Protector_PartitionsByTenantAndKeyButComparesCanonicalIntent()
    {
        IdempotencyKeyProtector protector = CreateProtector();
        CanonicalIdempotencyDescriptor first = Descriptor("target-a");
        CanonicalIdempotencyDescriptor different = Descriptor("target-b");

        IdempotencyProtectedIdentity firstIdentity = protector.Protect("tenant-a", "opaque-secret-key", first);
        IdempotencyProtectedIdentity equivalentIdentity = protector.Protect("tenant-a", "opaque-secret-key", first);
        IdempotencyProtectedIdentity differentIntentIdentity = protector.Protect("tenant-a", "opaque-secret-key", different);
        IdempotencyProtectedIdentity otherTenantIdentity = protector.Protect("tenant-b", "opaque-secret-key", first);

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
        IdempotencyAdmissionRecord? compacted = null;
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, existing));
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.StateName,
                Arg.Do<IdempotencyAdmissionRecord>(record => compacted = record),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        IdempotencyAdmissionResult result = await actor.AdmitAsync(request);

        result.Decision.ShouldBe(IdempotencyAdmissionDecision.Expired);
        compacted.ShouldNotBeNull().State.ShouldBe(IdempotencyAdmissionState.Expired);
        compacted.IntentDigest.ShouldBeNull();
        compacted.ReplayResult.ShouldBeNull();
        compacted.ReplayExpiresAt.ShouldBe(_now);
        compacted.TenantPartition.ShouldBe(request.TenantPartition);
        compacted.KeyDigest.ShouldBe(request.KeyDigest);
        compacted.VerificationTag.ShouldBe(request.VerificationTag);
        compacted.ToString().ShouldNotContain("protected-result");
        compacted.ToString().ShouldNotContain("original-intent");
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
        IdempotencyAdmissionRecord? compacted = null;
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(
                IdempotencyAdmissionActor.StateName,
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionRecord>(true, existing));
        _ = stateManager.SetStateAsync(
                IdempotencyAdmissionActor.StateName,
                Arg.Do<IdempotencyAdmissionRecord>(record => compacted = record),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        IdempotencyAdmissionResult result = await actor.AdmitAsync(Request());

        result.Decision.ShouldBe(IdempotencyAdmissionDecision.Expired);
        compacted.ShouldNotBeNull().LastObservedAt.ShouldBe(_now);
        compacted.State.ShouldBe(IdempotencyAdmissionState.Expired);
    }

    [Fact]
    public async Task AdmitAsync_VerificationTagMismatchFailsClosedAsCorrupt()
    {
        (IdempotencyAdmissionActor actor, IActorStateManager stateManager, _) = CreateActor();
        IdempotencyAdmissionRecord existing = Record() with { VerificationTag = "collision-tag" };
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
        var options = new IdempotencyAdmissionOptions
        {
            Enabled = true,
            ActiveDigestKeyVersion = "v1",
            DigestKeys = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["v1"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef")),
            },
            Operations = new Dictionary<string, IdempotencyAdmissionOperationOptions>(StringComparer.Ordinal)
            {
                ["folders:CreateFolder"] = new()
                {
                    DescriptorVersion = 1,
                    RetentionTier = IdempotencyReplayRetentionTier.Mutation,
                },
            },
        };

        return new IdempotencyKeyProtector(Options.Create(options));
    }

    private static CanonicalIdempotencyDescriptor Descriptor(string target)
        => new(
            "folders",
            "CreateFolder",
            1,
            Encoding.UTF8.GetBytes($"operation\0CreateFolder\0target\0{target}"),
            IdempotencyReplayRetentionTier.Mutation);

    private static IdempotencyAdmissionRequest Request(string intentDigest = "intent-a")
        => new(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            "tenant-a",
            "v1",
            "key-digest",
            "verification-tag",
            intentDigest,
            IdempotencyReplayRetentionTier.Mutation);

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
            replayExpiresAt ?? _now.AddHours(1),
            7,
            replayResult);
}
