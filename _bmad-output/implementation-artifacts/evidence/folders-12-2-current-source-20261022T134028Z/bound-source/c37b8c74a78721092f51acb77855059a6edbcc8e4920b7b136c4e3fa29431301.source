using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Builders;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Actors;

/// <summary>Exercises tenant/key admission against the real Dapr actor runtime and Redis state store.</summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public class IdempotencyAdmissionLiveSidecarTests(DaprTestContainerFixture fixture)
{
    [Fact]
    public async Task MultiHostAdmission_PrimaryHostRemovedBeforeExecution_ExecutesAndReplaysExactlyOnceOnReplica()
    {
        const string RawKey = "live-multi-host-idempotency-key";
        fixture.ResetTestState();
        fixture.SetupCounterDomain();
        await fixture.EnsureReplicaAsync();

        bool primaryStopped = false;
        try
        {
            IIdempotencyAdmissionCoordinator primaryCoordinator = fixture.Services
                .GetRequiredService<IIdempotencyAdmissionCoordinator>();
            IIdempotencyAdmissionCoordinator replicaCoordinator = fixture.ReplicaServices
                .GetRequiredService<IIdempotencyAdmissionCoordinator>();
            string aggregateId = $"multi-host-{Guid.NewGuid():N}";
            var firstRequest = new SubmitCommand(
                MessageId: "01J88888888888888888888888",
                Tenant: "tenant-multi-host",
                Domain: "counter",
                AggregateId: aggregateId,
                CommandType: "IncrementCounter",
                Payload: "{}"u8.ToArray(),
                CorrelationId: "multi-host-correlation-0",
                UserId: "live-test-user",
                IdempotencyKey: RawKey);

            IdempotencyAdmissionSession[] admissions = await Task.WhenAll(
                Enumerable.Range(0, 8).Select(async index =>
                {
                    IIdempotencyAdmissionCoordinator coordinator = index % 2 == 0
                        ? primaryCoordinator
                        : replicaCoordinator;
                    return await coordinator.AdmitAsync(
                        firstRequest with
                        {
                            MessageId = $"01J8888888888888888888888{index}",
                            CorrelationId = $"multi-host-correlation-{index}",
                        }) ?? throw new InvalidOperationException("Idempotency admission returned no session.");
                }));

            admissions.Count(result => result.Decision == IdempotencyAdmissionDecision.Execute).ShouldBe(1);
            admissions.Count(result => result.Decision == IdempotencyAdmissionDecision.Pending).ShouldBe(7);
            IdempotencyAdmissionSession executable = admissions.Single(result =>
                result.Decision == IdempotencyAdmissionDecision.Execute);
            admissions.Select(result => result.FencingToken).Distinct().ShouldBe([executable.FencingToken]);
            admissions.Select(result => result.ExecutionMessageId).Distinct().ShouldBe([executable.ExecutionMessageId]);
            admissions.Select(result => result.ExecutionCorrelationId).Distinct().ShouldBe([executable.ExecutionCorrelationId]);

            // Remove one complete application host and sidecar after the durable reservation but
            // before Begin/domain execution. The replica must finish the same fenced workflow.
            await fixture.StopPrimaryHostAndSidecarAsync();
            primaryStopped = true;
            await Task.Delay(2000);

            var executionRequest = firstRequest with
            {
                MessageId = executable.ExecutionMessageId!,
                CorrelationId = executable.ExecutionCorrelationId!,
                IdempotencyKey = null,
            };
            await replicaCoordinator.BeginAsync(executable);
            await replicaCoordinator.ValidateExecutionAsync(executable, executionRequest);
            ICommandRouter replicaRouter = fixture.ReplicaServices.GetRequiredService<ICommandRouter>();
            CommandProcessingResult terminal = await replicaRouter.RouteFencedCommandAsync(
                executionRequest,
                executable.ExecutionContext!);
            await replicaCoordinator.CompleteAsync(executable, terminal);

            var retry = firstRequest with
            {
                MessageId = "01J99999999999999999999999",
                CorrelationId = "retry-after-primary-removal",
            };
            IdempotencyAdmissionSession replay = (await replicaCoordinator.AdmitAsync(retry))!;
            replay.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
            replay.ReplayResult.ShouldBe(terminal);
            replay.ExecutionMessageId.ShouldBe(executable.ExecutionMessageId);
            replay.ExecutionCorrelationId.ShouldBe(executable.ExecutionCorrelationId);

            fixture.DomainServiceInvoker.Invocations.Count.ShouldBe(1);
            fixture.EventPublisher.PublishCalls.Count.ShouldBe(1);
            fixture.EventPublisher.TotalEventsPublished.ShouldBe(1);
            terminal.Accepted.ShouldBeTrue();
            terminal.EventCount.ShouldBe(1);

            string persisted = await fixture.GetActorStateJsonAsync(
                IdempotencyAdmissionActor.ActorTypeName,
                executable.ActorId,
                IdempotencyAdmissionActor.StateName);
            persisted.ShouldContain("\"replayExpiresAt\"");
            persisted.ShouldContain("\"replayResult\"");
            persisted.ShouldContain(executable.ExecutionMessageId!);
            persisted.ShouldNotContain(RawKey);
        }
        finally
        {
            await fixture.StopReplicaHostAndSidecarAsync();
            if (primaryStopped)
            {
                await fixture.RestartHostAndSidecarAsync();
            }
        }
    }

    [Fact]
    public async Task ConcurrentEquivalentAdmissions_ExecuteOnceAndPersistReplayableTerminalState()
    {
        const string RawKeyLeakSentinel = "raw-idempotency-key-must-never-persist";
        string keyDigest = $"digest-{Guid.NewGuid():N}";
        string actorId = $"tenant-a:v1:{keyDigest}";
        var factory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = fixture.DaprHttpEndpoint,
            RequestTimeout = TimeSpan.FromSeconds(15),
        });
        IIdempotencyAdmissionActor proxy = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
            new ActorId(actorId),
            IdempotencyAdmissionActor.ActorTypeName);
        var request = new IdempotencyAdmissionRequest(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            "tenant-a",
            "v1",
            keyDigest,
            $"tag-{Guid.NewGuid():N}",
            $"intent-{Guid.NewGuid():N}",
            IdempotencyReplayRetentionTier.Mutation,
            "01J00000000000000000000000",
            "trace-concurrent");

        IdempotencyAdmissionResult[] concurrent = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => proxy.AdmitAsync(request)));

        concurrent.Count(result => result.Decision == IdempotencyAdmissionDecision.Execute).ShouldBe(1);
        concurrent.Count(result => result.Decision == IdempotencyAdmissionDecision.Pending).ShouldBe(7);
        long fence = concurrent.Single(result => result.Decision == IdempotencyAdmissionDecision.Execute).FencingToken;
        await proxy.BeginAsync(new IdempotencyAdmissionTransitionRequest(fence));
        var completed = new CommandProcessingResult(
            true,
            CorrelationId: "original-correlation",
            EventCount: 1,
            ResultPayload: "{\"ok\":true}");
        await proxy.CompleteAsync(new IdempotencyAdmissionCompletionRequest(fence, completed));

        IdempotencyAdmissionResult replay = await proxy.AdmitAsync(request);
        replay.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
        replay.ReplayResult.ShouldBe(completed);
        IdempotencyAdmissionResult conflict = await proxy.AdmitAsync(request with
        {
            IntentDigest = $"different-{Guid.NewGuid():N}",
        });
        conflict.Decision.ShouldBe(IdempotencyAdmissionDecision.Conflict);

        string persisted = await fixture.GetActorStateJsonAsync(
            IdempotencyAdmissionActor.ActorTypeName,
            actorId,
            IdempotencyAdmissionActor.StateName);
        persisted.ShouldContain(keyDigest);
        persisted.ShouldContain(request.VerificationTag);
        persisted.ShouldContain(request.IntentDigest);
        persisted.ShouldContain("original-correlation");
        persisted.ShouldNotContain(RawKeyLeakSentinel);
    }

    [Fact]
    public async Task TerminalAdmission_SurvivesApplicationAndSidecarRestart()
    {
        string keyDigest = $"restart-digest-{Guid.NewGuid():N}";
        string actorId = $"tenant-restart:v1:{keyDigest}";
        var request = new IdempotencyAdmissionRequest(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            "tenant-restart",
            "v1",
            keyDigest,
            $"tag-{Guid.NewGuid():N}",
            $"intent-{Guid.NewGuid():N}",
            IdempotencyReplayRetentionTier.Mutation,
            "01J11111111111111111111111",
            "trace-restart");
        IIdempotencyAdmissionActor proxy = CreateProxy(actorId);

        IdempotencyAdmissionResult admitted = await proxy.AdmitAsync(request);
        admitted.Decision.ShouldBe(IdempotencyAdmissionDecision.Execute);
        await proxy.BeginAsync(new IdempotencyAdmissionTransitionRequest(admitted.FencingToken));
        var completed = new CommandProcessingResult(
            true,
            CorrelationId: "before-restart",
            EventCount: 1,
            ResultPayload: "{\"persisted\":true}");
        await proxy.CompleteAsync(new IdempotencyAdmissionCompletionRequest(admitted.FencingToken, completed));

        await fixture.RestartHostAndSidecarAsync();

        IdempotencyAdmissionResult replay = await CreateProxy(actorId).AdmitAsync(request);
        replay.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
        replay.ReplayResult.ShouldBe(completed);

        string persisted = await fixture.GetActorStateJsonAsync(
            IdempotencyAdmissionActor.ActorTypeName,
            actorId,
            IdempotencyAdmissionActor.StateName);
        persisted.ShouldContain("before-restart");
    }

    [Fact]
    public async Task LegacyMigration_TargetRedirectInventoryAndDirectoryRemainProvenAfterRestart()
    {
        const string RawKeyLeakSentinel = "legacy-raw-key-must-not-leak";
        const string CanonicalIntentLeakSentinel = "legacy-canonical-intent-must-not-leak";
        fixture.ResetTestState();
        fixture.SetupCounterDomain();
        string suffix = Guid.NewGuid().ToString("N");
        string tenant = $"legacy-{suffix}";
        CommandEnvelope command = new CommandEnvelopeBuilder()
            .WithTenantId(tenant)
            .WithDomain("counter")
            .WithAggregateId($"aggregate-{suffix}")
            .WithCommandType("IncrementCounter")
            .WithMessageId("01J77777777777777777777777")
            .WithCorrelationId("legacy-restart-trace")
            .WithCausationId("01J77777777777777777777777")
            .Build();
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = fixture.DaprHttpEndpoint,
            RequestTimeout = TimeSpan.FromSeconds(15),
        });
        IAggregateActor aggregate = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(command.AggregateIdentity.ActorId),
            fixture.AggregateActorTypeName);
        CommandProcessingResult logicalResult = await aggregate.ProcessCommandAsync(command);
        string sourceJson = await fixture.GetActorStateJsonAsync(
            fixture.AggregateActorTypeName,
            command.AggregateIdentity.ActorId,
            $"idempotency:{command.MessageId}");
        IdempotencyRecord sourceRecord = JsonSerializer.Deserialize<IdempotencyRecord>(
            sourceJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Legacy source record did not deserialize.");
        string keyDigest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(RawKeyLeakSentinel)));
        string intentDigest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalIntentLeakSentinel)));
        string targetActorId = $"{tenant}:v1:{keyDigest}";
        var alias = new IdempotencyAdmissionDirectoryAlias("v1", targetActorId, keyDigest);
        var entry = new IdempotencyLegacyInventoryEntry(
            IdempotencyLegacyInventoryEntry.CurrentSchemaVersion,
            tenant,
            command.AggregateIdentity.ActorId,
            IdempotencyLegacySourceEvidence.Compute(sourceRecord),
            1,
            alias.DigestKeyVersion,
            alias.KeyDigest,
            $"verification-{suffix}",
            intentDigest,
            IdempotencyReplayRetentionTier.Mutation,
            sourceRecord.ProcessedAt,
            sourceRecord.ProcessedAt,
            sourceRecord.ExpiresAt!.Value,
            logicalResult,
            command.MessageId,
            command.CorrelationId,
            IdempotencyLegacyMigrationPhase.Inventoried,
            $"inventory-{suffix}",
            1,
            $"migration-{suffix}");
        IIdempotencyLegacyInventoryActor inventory = actorProxyFactory
            .CreateActorProxy<IIdempotencyLegacyInventoryActor>(
                new ActorId(tenant),
                IdempotencyLegacyInventoryActor.ActorTypeName);
        await inventory.InventoryAsync(entry);
        await inventory.CloseAsync(
            new IdempotencyLegacyInventoryClosure(
                IdempotencyLegacyInventoryClosure.CurrentSchemaVersion,
                tenant,
                entry.InventoryId,
                entry.InventoryVersion,
                [entry.DigestKeyVersion],
                1,
                IdempotencyLegacyInventoryEvidence.ComputeManifestDigest(
                    IdempotencyLegacyInventoryClosure.CurrentSchemaVersion,
                    tenant,
                    entry.InventoryId,
                    entry.InventoryVersion,
                    [IdempotencyLegacyInventoryEvidence.ComputeEntryDigest(entry)],
                    [entry.DigestKeyVersion])));
        var targetReference = new IdempotencyTenantLifecycleReference(
            targetActorId,
            alias.DigestKeyVersion,
            alias.KeyDigest);
        IIdempotencyTenantLifecycleActor lifecycle = actorProxyFactory
            .CreateActorProxy<IIdempotencyTenantLifecycleActor>(
                new ActorId(tenant),
                IdempotencyTenantLifecycleActor.ActorTypeName);
        await lifecycle.RegisterAsync([targetReference]);
        var migrationRequest = new IdempotencyLegacyMigrationRequest(
            [alias],
            targetReference,
            entry.VerificationTag,
            entry.IntentDigest,
            entry.RetentionTier,
            entry.VerificationTag,
            entry.IntentDigest,
            entry.RetentionTier);
        IIdempotencyTenantLifecycleMigrationActor migration = actorProxyFactory
            .CreateActorProxy<IIdempotencyTenantLifecycleMigrationActor>(
                new ActorId(tenant),
                IdempotencyTenantLifecycleActor.ActorTypeName);
        IdempotencyLegacyMigrationResult migrated = await migration.MigrateLegacyAsync(migrationRequest);
        migrated.DeniedDecision.ShouldBeNull();
        migrated.TargetAdmissionActorId.ShouldBe(targetActorId);
        var sourceRequest = new IdempotencyLegacySourceRequest(
            IdempotencyLegacySourceRequest.CurrentSchemaVersion,
            tenant,
            entry.InventoryId,
            entry.MigrationId,
            entry.LegacySchemaVersion,
            entry.SourceEvidenceDigest,
            entry.ExecutionMessageId,
            entry.ExecutionCorrelationId,
            entry.FirstConsumedAt,
            entry.ReplayExpiresAt,
            entry.ReplayResult);
        var targetRequest = new IdempotencyAdmissionRequest(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            tenant,
            alias.DigestKeyVersion,
            alias.KeyDigest,
            entry.VerificationTag,
            entry.IntentDigest,
            entry.RetentionTier,
            entry.ExecutionMessageId,
            entry.ExecutionCorrelationId);
        IdempotencyAdmissionResult firstReplay = await CreateProxy(targetActorId).AdmitAsync(targetRequest);
        firstReplay.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
        firstReplay.ReplayResult.ShouldBe(logicalResult);

        await fixture.RestartHostAndSidecarAsync();

        IdempotencyLegacyMigrationResult reprovedMigration = await new ActorProxyFactory(
            new ActorProxyOptions
            {
                HttpEndpoint = fixture.DaprHttpEndpoint,
                RequestTimeout = TimeSpan.FromSeconds(15),
            }).CreateActorProxy<IIdempotencyTenantLifecycleMigrationActor>(
                new ActorId(tenant),
                IdempotencyTenantLifecycleActor.ActorTypeName)
            .MigrateLegacyAsync(migrationRequest);
        IdempotencyLegacyInventoryInspection inventoryProof = await CreateLegacyInventoryProxy(tenant)
            .InspectAsync([alias]);
        IdempotencyLegacySourceInspection sourceProof = await CreateLegacySourceProxy(entry.SourceAggregateActorId)
            .InspectLegacySourceAsync(sourceRequest);
        IdempotencyAdmissionPromotionAcknowledgement targetProof = await CreateProxy(targetActorId)
            .AcknowledgePromotionAsync(
                new IdempotencyAdmissionPromotionAcknowledgementRequest(
                    entry.SourceAggregateActorId,
                    entry.MigrationId,
                    entry.SourceEvidenceDigest,
                    inventoryProof.Entry!.TargetImportDigest!));
        IdempotencyAdmissionDirectoryResult directoryProof = await CreateDirectoryInspectionProxy(tenant)
            .InspectAsync([alias])
            ?? throw new InvalidOperationException("Legacy migration directory proof is missing.");
        IdempotencyAdmissionResult restartedReplay = await CreateProxy(targetActorId).AdmitAsync(targetRequest);
        reprovedMigration.DeniedDecision.ShouldBeNull();
        reprovedMigration.TargetAdmissionActorId.ShouldBe(targetActorId);
        inventoryProof.Decision.ShouldBe(IdempotencyLegacyInventoryDecision.Migrated);
        sourceProof.Decision.ShouldBe(IdempotencyLegacySourceDecision.Redirected);
        sourceProof.RedirectDigest.ShouldBe(inventoryProof.Entry!.SourceRedirectDigest);
        targetProof.Activated.ShouldBeTrue();
        directoryProof.CanonicalActorId.ShouldBe(targetActorId);
        restartedReplay.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
        restartedReplay.ReplayResult.ShouldBe(logicalResult);
        fixture.DomainServiceInvoker.Invocations.Count.ShouldBe(1);
        string persistedProof = string.Join(
            '\n',
            await fixture.GetActorStateJsonAsync(
                IdempotencyAdmissionActor.ActorTypeName,
                targetActorId,
                IdempotencyAdmissionActor.PromotionStateName),
            await fixture.GetActorStateJsonAsync(
                fixture.AggregateActorTypeName,
                entry.SourceAggregateActorId,
                IdempotencyChecker.GetLegacyRedirectKey(entry.ExecutionMessageId)),
            await fixture.GetActorStateJsonAsync(
                IdempotencyLegacyInventoryActor.ActorTypeName,
                tenant,
                $"legacy:{entry.DigestKeyVersion}:{entry.KeyDigest}"),
            await fixture.GetActorStateJsonAsync(
                IdempotencyAdmissionDirectoryActor.ActorTypeName,
                tenant,
                IdempotencyAdmissionDirectoryActor.BuildStateName(alias)));
        persistedProof.ShouldNotContain(RawKeyLeakSentinel);
        persistedProof.ShouldNotContain(CanonicalIntentLeakSentinel);
    }

    private IIdempotencyAdmissionActor CreateProxy(string actorId)
    {
        var factory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = fixture.DaprHttpEndpoint,
            RequestTimeout = TimeSpan.FromSeconds(15),
        });
        return factory.CreateActorProxy<IIdempotencyAdmissionActor>(
            new ActorId(actorId),
            IdempotencyAdmissionActor.ActorTypeName);
    }

    private IIdempotencyLegacyInventoryActor CreateLegacyInventoryProxy(string tenant)
        => new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = fixture.DaprHttpEndpoint,
            RequestTimeout = TimeSpan.FromSeconds(15),
        }).CreateActorProxy<IIdempotencyLegacyInventoryActor>(
            new ActorId(tenant),
            IdempotencyLegacyInventoryActor.ActorTypeName);

    private IIdempotencyLegacySourceActor CreateLegacySourceProxy(string actorId)
        => new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = fixture.DaprHttpEndpoint,
            RequestTimeout = TimeSpan.FromSeconds(15),
        }).CreateActorProxy<IIdempotencyLegacySourceActor>(
            new ActorId(actorId),
            fixture.AggregateActorTypeName);

    private IIdempotencyAdmissionDirectoryInspectionActor CreateDirectoryInspectionProxy(string tenant)
        => new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = fixture.DaprHttpEndpoint,
            RequestTimeout = TimeSpan.FromSeconds(15),
        }).CreateActorProxy<IIdempotencyAdmissionDirectoryInspectionActor>(
            new ActorId(tenant),
            IdempotencyAdmissionDirectoryActor.ActorTypeName);

}
