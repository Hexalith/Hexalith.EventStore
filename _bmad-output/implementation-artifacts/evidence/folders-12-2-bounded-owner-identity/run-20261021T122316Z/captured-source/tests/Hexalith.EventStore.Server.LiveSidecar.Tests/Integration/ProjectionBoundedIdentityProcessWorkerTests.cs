using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Runs the public production dispatcher in a replaceable operating-system process.</summary>
public sealed class ProjectionBoundedIdentityProcessWorkerTests
{
    [Fact]
    public async Task RunBoundedOwnerWorkerAsync()
    {
        string? phase = Environment.GetEnvironmentVariable("BOUNDED_IDENTITY_PHASE");
        if (phase is null) { Assert.Skip("Launched only by the bounded identity process qualification."); return; }
        string tenant = Required("BOUNDED_IDENTITY_TENANT"), endpoint = Required("BOUNDED_IDENTITY_ENDPOINT");
        int count = int.Parse(Required("BOUNDED_IDENTITY_COUNT"), System.Globalization.CultureInfo.InvariantCulture);
        string evidence = Required("BOUNDED_IDENTITY_EVIDENCE");
        using var client = new DaprClientBuilder().UseGrpcEndpoint(endpoint).Build();
        var store = new ProjectionBoundedIdentityLiveStore(client, phase == "promote" ? "identity-seed:" : null);
        bool bufferedOwner = Required("BOUNDED_IDENTITY_BUFFERED") == "true";
        IAsyncDomainSharedProjectionStreamingRebuildHandler handler = bufferedOwner
            ? new ProjectionBoundedIdentityBufferedLiveHandler() : new ProjectionBoundedIdentityLiveHandler();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var scope = handler.CreateScope(tenant);
        var services = new ServiceCollection();
        services.AddSingleton<IReadModelStore>(store);
        services.AddSingleton<IReadModelBatchStore>(store);
        services.AddSingleton(coordinator);
        services.AddSingleton<IAsyncDomainProjectionHandler>(handler);
        using var provider = services.BuildServiceProvider();
        string catalog = ProjectionRouteCatalogFingerprint.Compute("bounded-worker", "v1", [new("widget", handler.ProjectionType)]);
        var identity = new DomainSharedProjectionRebuildIdentity(tenant, "widget", handler.ProjectionType, "original", catalog);
        var options = new ProjectionDispatchOptions { MaxRebuildEventCount = 40_000 };
        var local = new DomainProjectionIdentityOptions { AppId = "bounded-worker", ServiceVersion = "v1" };
        Task<DomainSharedProjectionRebuildResponse> Send(DomainSharedProjectionRebuildRequest request)
            => DomainSharedProjectionRebuildDispatcher.DispatchAsync(provider, request, options, local, TestContext.Current.CancellationToken);
        DomainSharedProjectionRebuildRequest Action(DomainSharedProjectionRebuildAction action) => new(DomainSharedProjectionRebuildProtocol.Version, action, identity);
        void WriteEvidence()
        {
            var payload = new JsonObject
            {
                ["bufferedOwner"] = bufferedOwner, ["phase"] = phase, ["pid"] = Environment.ProcessId, ["count"] = count, ["tenant"] = tenant,
                ["measurements"] = JsonSerializer.SerializeToNode(store.Measurements), ["runtime"] = ProjectionBoundedIdentityRuntimeProof.Capture(),
                ["buildGraph"] = "Debug;UseHexalithProjectReferences=true;net10.0"
            };
            File.WriteAllText(evidence, payload.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        store.BeforeProcessExit = WriteEvidence;
        if (phase == "stage")
        {
            _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
            foreach (string owner in new[] { "owner-a", "owner-b" })
                for (int first = 1; first <= OwnerCount(owner); first += 256)
                    await store.SaveAsync("statestore", SourceKey(owner, first), Enumerable.Range(first, Math.Min(256, OwnerCount(owner) - first + 1)).Select(sequence => Event(owner, sequence)).ToArray());
            var accumulated = await AccumulateAllAsync();
            (await Send(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Finalize, identity,
                ExpectedAggregateCount: 2, ExpectedInventoryFingerprint: accumulated.InventoryFingerprint))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            (await Send(Action(DomainSharedProjectionRebuildAction.Stage))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            (await coordinator.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(0);
            WriteEvidence();
            Environment.Exit(73);
        }
        if (phase == "promote")
        {
            (await Send(Action(DomainSharedProjectionRebuildAction.Stage))).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
            _ = await Send(Action(DomainSharedProjectionRebuildAction.Commit));
            throw new InvalidOperationException("Promoted handoff did not reach its armed real process interruption.");
        }
        phase.ShouldBe("resume");
        (await coordinator.GetStatusAsync(scope)).ActiveGeneration.ShouldBeGreaterThan(0);
        (await Send(Action(DomainSharedProjectionRebuildAction.Commit))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await Send(Action(DomainSharedProjectionRebuildAction.Verify))).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        var retained = (await coordinator.GetCheckpointAsync(scope, "owner-a"))!.CapturedIdentityReference.ShouldNotBeNull();
        await VerifyOwnersAsync(count);
        foreach (int sequence in new[] { 1, count / 2, count })
            (await OrdinaryAsync(Event("owner-a", sequence))).ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await OrdinaryAsync(Event("owner-b", 1))).ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        var growth = Event("owner-a", count + 1);
        await store.SaveAsync("statestore", SourceKey("owner-a", count + 1) + ":growth", new[] { growth });
        (await OrdinaryAsync(growth)).ShouldBe(ProjectionDispatchStatus.Completed);
        (await coordinator.GetCheckpointAsync(scope, "owner-a"))!.CapturedIdentityReference.ShouldBe(retained);
        identity = identity with { OperationId = "grown" };
        var grown = await AccumulateAllAsync(includeGrowth: true);
        (await Send(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Finalize, identity,
            ExpectedAggregateCount: 2, ExpectedInventoryFingerprint: grown.InventoryFingerprint))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await Send(Action(DomainSharedProjectionRebuildAction.Stage))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await Send(Action(DomainSharedProjectionRebuildAction.Commit))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await Send(Action(DomainSharedProjectionRebuildAction.Verify))).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        await VerifyOwnersAsync(count + 1);
        await coordinator.CapturedIdentities.ValidateCompleteAsync(scope, retained);
        foreach (int sequence in new[] { 1, count / 2, count, count + 1 })
            (await OrdinaryAsync(Event("owner-a", sequence))).ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await OrdinaryAsync(Event("owner-a", 1) with { MessageId = "contradictory-original" })).ShouldBe(ProjectionDispatchStatus.Failed);
        (await coordinator.GetStatusAsync(scope)).DeliveryConflicts.ShouldNotBeEmpty();
        store.Measurements.MaximumBatchOperations.ShouldBeLessThanOrEqualTo(100);
        store.Measurements.MaximumCanonicalBatchBytes.ShouldBeLessThanOrEqualTo(1_048_576);
        store.Measurements.MaximumPhysicalValueBytes.ShouldBeLessThanOrEqualTo(1_048_576);
        store.Measurements.MaximumIdentityValueBytes.ShouldBeLessThan(65_536);
        WriteEvidence();

        int OwnerCount(string owner) => owner == "owner-a" ? count : 129;
        string SourceKey(string owner, int first) => "bounded-source:" + tenant + ":" + owner + ":" + first;
        ProjectionEventDto Event(string owner, int sequence) => new("changed", BitConverter.GetBytes(sequence), "json", sequence,
            DateTimeOffset.UnixEpoch.AddSeconds(sequence), "correlation", "message-" + sequence, "user", sequence * 3L + (owner == "owner-b" ? 1 : 0));
        async IAsyncEnumerable<ProjectionEventDto[]> SourcePages(string owner, bool includeGrowth)
        {
            for (int first = 1; first <= OwnerCount(owner); first += 256)
            {
                var page = (await store.GetAsync<ProjectionEventDto[]>("statestore", SourceKey(owner, first))).Value.ShouldNotBeNull();
                page.Length.ShouldBe(Math.Min(256, OwnerCount(owner) - first + 1));
                for (int index = 0; index < page.Length; index++) JsonSerializer.Serialize(page[index]).ShouldBe(JsonSerializer.Serialize(Event(owner, first + index)));
                yield return page;
            }
            if (owner == "owner-a" && includeGrowth)
                yield return (await store.GetAsync<ProjectionEventDto[]>("statestore", SourceKey(owner, count + 1) + ":growth")).Value.ShouldNotBeNull();
        }
        async Task<DomainSharedProjectionRebuildResponse> AccumulateAllAsync(bool includeGrowth = false)
        {
            var fingerprints = new Dictionary<string, string>();
            string inventory = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
            int ordinal = 0;
            foreach (string owner in new[] { "owner-a", "owner-b" })
            {
                using var hash = new DomainSharedProjectionHistoryFingerprintBuilder(owner, false, OwnerCount(owner) + (includeGrowth && owner == "owner-a" ? 1 : 0));
                await foreach (var page in SourcePages(owner, includeGrowth)) foreach (var item in page) hash.Append(item);
                fingerprints[owner] = hash.Finish();
                inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(inventory, ordinal++, fingerprints[owner]);
            }
            (await Send(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin, identity,
                CaptureInventoryFingerprint: inventory, SourceHighWatermarks: new Dictionary<string, long>
                { ["owner-a"] = (count + (includeGrowth ? 1 : 0)) * 3L, ["owner-b"] = 129 * 3L + 1 }))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            DomainSharedProjectionRebuildResponse? accepted = null;
            ordinal = 0;
            foreach (string owner in new[] { "owner-a", "owner-b" })
            {
                int part = 0;
                await foreach (var page in SourcePages(owner, includeGrowth))
                    (await Send(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulatePart, identity,
                        AggregateOrdinal: ordinal, AggregateId: owner, Events: page, PartOrdinal: part++))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
                accepted = await Send(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulateComplete, identity,
                    AggregateOrdinal: ordinal++, AggregateId: owner, PartCount: part, ExpectedHistoryFingerprint: fingerprints[owner]));
                accepted.Status.ShouldBe(ProjectionDispatchStatus.Completed);
            }
            return accepted!;
        }
        async Task VerifyOwnersAsync(int expected)
        {
            foreach (string owner in new[] { "owner-a", "owner-b" })
            {
                long head = owner == "owner-a" ? expected : 129;
                var checkpoint = (await coordinator.GetCheckpointAsync(scope, owner)).ShouldNotBeNull();
                checkpoint.OwnerSequenceNumber.ShouldBe(head);
                checkpoint.CapturedEventDigests.ShouldBeNull();
                checkpoint.CapturedIdentityReference!.Count.ShouldBe(head);
                var row = (await coordinator.ReadAsync<JsonObject>(scope, "row:" + owner)).Value.ShouldNotBeNull();
                row["count"]!.GetValue<long>().ShouldBe(head);
                row["last"]!.GetValue<long>().ShouldBe(head * 3 + (owner == "owner-b" ? 1 : 0));
            }
        }
        async Task<ProjectionDispatchStatus> OrdinaryAsync(ProjectionEventDto item)
        {
            string owner = item.GlobalPosition % 3 == 1 ? "owner-b" : "owner-a";
            var response = await DomainProjectionDispatcher.DispatchAsync(provider, new ProjectionDispatchRequest(
                new ProjectionRequest(tenant, "widget", owner, [item]), [handler.ProjectionType], item.MessageId, catalog), options, local, TestContext.Current.CancellationToken);
            return response.Outcomes.Single().Status;
        }
    }
    private static string Required(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException("Missing worker input " + name);
}
