using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Scenario = Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.ProjectionExpiryAndScratchRecoveryLiveTests;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Executes one named scratch phase in its own OS process against the parent's real sidecar.</summary>
public sealed class ProjectionScratchProcessWorkerTests
{
    [Fact]
    public async Task PersistOrRecoverOriginalNamedOwnerScratch()
    {
        string? phase = Environment.GetEnvironmentVariable("EXPIRY_SCRATCH_PROCESS_PHASE");
        if (phase is null)
        {
            Assert.Skip("Worker is launched by PendingNamedOwnerScratchRecoversAfterOwningOsProcessExits only.");
            return;
        }
        phase.ShouldBeOneOf("persist", "resume");
        string tenant = Required("EXPIRY_SCRATCH_PROCESS_TENANT");
        bool populated = bool.Parse(Required("EXPIRY_SCRATCH_PROCESS_POPULATED"));
        bool retry = bool.Parse(Required("EXPIRY_SCRATCH_PROCESS_RETRY"));
        using DaprClient client = new DaprClientBuilder().UseGrpcEndpoint(Required("EXPIRY_SCRATCH_PROCESS_ENDPOINT")).Build();
        var store = new DaprReadModelStore(client);
        var handler = new LivePartitionedScratchHandler();
        SharedProjectionScope scope = handler.CreateScope(tenant);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Scenario.NamedProvider(store, store, coordinator, handler);
        DomainSharedProjectionRebuildIdentity identity = Scenario.NamedIdentity(tenant, "process-interrupted");
        ProjectionEventDto first = Scenario.NamedEvent(1);
        string session = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        string scratch = DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(session, 0, 0);
        var raw = new DaprReadModelBatchStateAccessor(client, "statestore");
        if (phase == "persist")
        {
            _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
            if (populated)
            {
                DomainSharedProjectionRebuildIdentity old = Scenario.NamedIdentity(tenant, "old");
                (await Scenario.NamedBegin(provider, old, [("owner-a", first)])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
                DomainSharedProjectionRebuildResponse accepted = await Scenario.NamedAccumulate(provider, old, "owner-a", first);
                (await Scenario.NamedFinish(provider, old, accepted)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
            }
            (await Scenario.NamedBegin(provider, identity, [("owner-a", first), ("owner-b", first)])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            ReadModelBatchPhase stop = retry ? ReadModelBatchPhase.AfterInstallOperation : ReadModelBatchPhase.BeforeCommit;
            var fault = new ScratchRecoveryLiveBatchStore(client, store, stop, retry ? 0 : -1, "owner-scratch:");
            using ServiceProvider interrupted = Scenario.NamedProvider(store, fault, new SharedProjectionEpochCoordinator(store, fault), handler);
            (await Scenario.NamedAccumulate(interrupted, identity, "owner-a", first)).Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
            RawStateEntry scratchBytes = await raw.ReadAsync(scratch, CancellationToken.None);
            RawStateEntry fenceBytes = await raw.ReadAsync(scope.StateKey, CancellationToken.None);
            (ReadModelBatchEnvelope.IsEnvelope(scratchBytes.Value.Span) || ReadModelBatchEnvelope.IsEnvelope(fenceBytes.Value.Span)).ShouldBeTrue();
            TestContext.Current.TestOutputHelper!.WriteLine($"phase=persist; pid={Environment.ProcessId}; pending-original-owner-scratch=true; stop={stop}; exits-without-repair=true");
            return;
        }
        (await coordinator.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(populated ? 1 : 0);
        ((await coordinator.ReadAsync<JsonObject>(scope, "row:owner-a")).Value?["count"]?.GetValue<int>()).ShouldBe(populated ? 1 : null);
        if (retry)
        {
            (await Scenario.NamedAccumulate(provider, identity, "owner-a", first)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            (await Scenario.NamedAccumulate(provider, identity, "owner-a", first)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        }
        DomainSharedProjectionRebuildResponse aborted = await Scenario.NamedDispatch(provider,
            new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Abort, identity));
        aborted.Status.ShouldBe(ProjectionDispatchStatus.Completed, aborted.ToString());
        aborted.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Aborted);
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope);
        status.IsBuilding.ShouldBeFalse(); status.IsAborting.ShouldBeFalse(); status.ActiveGeneration.ShouldBe(populated ? 1 : 0);
        ((await coordinator.ReadAsync<JsonObject>(scope, "row:owner-a")).Value?["count"]?.GetValue<int>()).ShouldBe(populated ? 1 : null);
        (await raw.ReadAsync(scratch, CancellationToken.None)).Exists.ShouldBeFalse();
        (await raw.ReadAsync(DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(session, 0), CancellationToken.None)).Exists.ShouldBeFalse();
        foreach (string owner in new[] { "owner-a", "owner-b" })
        {
            ProjectionEventDto growth = Scenario.NamedEvent(9) with { GlobalPosition = 2, SequenceNumber = populated && owner == "owner-a" ? 2 : 1, MessageId = "growth-" + owner };
            ProjectionDispatchResponse delivered = await DomainProjectionDispatcher.DispatchAsync(provider,
                new ProjectionDispatchRequest(new ProjectionRequest(tenant, "widget", owner, [growth]), [handler.ProjectionType], growth.MessageId, identity.CatalogFingerprint),
                new ProjectionDispatchOptions(), new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, CancellationToken.None);
            delivered.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed, delivered.ToString());
            (await coordinator.ReadAsync<JsonObject>(scope, "row:" + owner)).Value!["count"]!.GetValue<int>().ShouldBe(9);
        }
        DomainSharedProjectionRebuildIdentity fresh = Scenario.NamedIdentity(tenant, "fresh");
        ProjectionEventDto captured = Scenario.NamedEvent(9) with { GlobalPosition = 2, MessageId = "growth-owner-b" };
        (await Scenario.NamedBegin(provider, fresh, [("owner-b", captured)])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        DomainSharedProjectionRebuildResponse next = await Scenario.NamedAccumulate(provider, fresh, "owner-b", captured);
        (await Scenario.NamedFinish(provider, fresh, next)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.ReadAsync<JsonObject>(scope, "row:owner-b")).Value!["count"]!.GetValue<int>().ShouldBe(9);
        TestContext.Current.TestOutputHelper!.WriteLine($"phase=resume; pid={Environment.ProcessId}; retry-first={retry}; private-physically-cleaned=true; selected-generation-preserved-on-abort=true; ordinary-owners=2; fresh-named-rebuild=true");
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value : throw new InvalidOperationException("Missing owner-scratch process input " + name);
}
