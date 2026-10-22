using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed partial class ProjectionExpiryAndScratchRecoveryLiveTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    public async Task RetiredParticipantRecoversAfterLatePhysicalInstallerProcessExits(bool abort, int ordinal)
    {
        string tenant = "review4-exited-" + Guid.NewGuid().ToString("N");
        using DaprClient client = Client();
        var store = new DaprReadModelStore(client);
        var shared = new SharedProjectionScope("statestore", tenant, "widget", "review4", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(shared, "writer");
        _ = await coordinator.BeginAsync(shared, "original", "inventory", new Dictionary<string, long>());
        var state = await store.GetAsync<SharedProjectionEpochState>(shared.StoreName, shared.StateKey);
        string scratch = tenant + ":private";
        var batch = new ReadModelBatch(new(shared.StoreName, tenant, shared.Domain, shared.Family, shared.Family, "retained"),
            [ReadModelBatchOperation.Write(scratch, Value(1), ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(shared.StateKey, state.Value!, ReadModelBatchConcurrency.Match(state.ETag!))])
            { RetainManifestForRecovery = true };
        string directory = Environment.GetEnvironmentVariable("EVENTSTORE_SCRATCH_PROCESS_EVIDENCE_DIR") ?? Path.GetTempPath();
        Directory.CreateDirectory(directory);
        string input = Path.Combine(directory, tenant);
        await File.WriteAllBytesAsync(input + ".manifest", ReadModelBatchFingerprint.BuildCanonicalManifest(batch));
        using Process installer = StartWorker("install");
        Task<string> installOutput = installer.StandardOutput.ReadToEndAsync();
        Task<string> installError = installer.StandardError.ReadToEndAsync();
        try
        {
            using var readyBudget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (!File.Exists(input + ".ready")) await Task.Delay(25, readyBudget.Token);
            if (abort)
            {
                (await store.StageAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Prepared);
                (await store.AbortAsync(batch)).Status.ShouldBe(ReadModelBatchStagingStatus.Aborted);
            }
            else (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
            if (!abort)
            {
                var selected = await store.GetAsync<JsonObject>(shared.StoreName, scratch);
                (await store.TryEraseAsync(shared.StoreName, scratch, selected.ETag!)).ShouldBeTrue();
            }
            await coordinator.ReclaimScratchReceiptAsync(shared, batch.Scope.BatchId);
            await coordinator.AbortAsync(shared, "original", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
            byte[] opened = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(shared.StateKey), "data"))!;
            await File.WriteAllTextAsync(input + ".release", "release original physical write");
            await FinishWorker(installer, installOutput, installError, "install");
            byte[]? late = (byte[]?)(await _redis.GetDatabase().HashGetAsync(Key(batch.Operations[ordinal].Key), "data"));
            bool lateEnvelope = late is not null && ReadModelBatchEnvelope.IsEnvelope(late);
            if (ordinal == 0) lateEnvelope.ShouldBeTrue("the original installer must physically persist then exit before its post-check");
            else late.ShouldBe(opened, "the original epoch CAS must preserve the newer Open fence");
            using Process recovery = StartWorker("recover");
            await FinishWorker(recovery, recovery.StandardOutput.ReadToEndAsync(), recovery.StandardError.ReadToEndAsync(), "recover");
            recovery.Id.ShouldNotBe(installer.Id);
            (await _redis.GetDatabase().KeyExistsAsync(Key(scratch))).ShouldBeFalse();
            ((byte[])(await _redis.GetDatabase().HashGetAsync(Key(shared.StateKey), "data"))!).ShouldBe(opened);
            (await coordinator.GetStatusAsync(shared)).IsBuilding.ShouldBeFalse();
            _ = await coordinator.BeginAsync(shared, "fresh", "fresh-inventory", new Dictionary<string, long>());
            await coordinator.AbortAsync(shared, "fresh", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
            TestContext.Current.TestOutputHelper!.WriteLine(JsonSerializer.Serialize(new
            { observation = "review4-exited-installer", abort, ordinal, installer_pid = installer.Id, recovery_pid = recovery.Id,
                installer_exited_before_recovery = true, late_envelope = lateEnvelope, late_base64 = late is null ? null : Convert.ToBase64String(late),
                exact_open_preserved = true, private_absent = true, fresh_epoch_cycle = true }));
        }
        finally { if (!installer.HasExited) installer.Kill(entireProcessTree: true); }

        Process StartWorker(string phase)
        {
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string argument in new[] { typeof(ProjectionRetiredInstallerProcessWorkerTests).Assembly.Location, "-class",
                typeof(ProjectionRetiredInstallerProcessWorkerTests).FullName!, "-showliveoutput", "-result-xml", input + "." + phase + ".xml" }) start.ArgumentList.Add(argument);
            start.Environment["REVIEW4_INSTALLER_INPUT"] = input;
            start.Environment["REVIEW4_INSTALLER_ENDPOINT"] = fixture.DaprGrpcEndpoint;
            start.Environment["REVIEW4_INSTALLER_TENANT"] = tenant;
            start.Environment["REVIEW4_INSTALLER_PHASE"] = phase;
            start.Environment["REVIEW4_INSTALLER_ORDINAL"] = ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            File.WriteAllText(input + "." + phase + ".command.json", JsonSerializer.Serialize(new { command = new[] { start.FileName }.Concat(start.ArgumentList),
                environment = start.Environment.Where(pair => pair.Key.StartsWith("REVIEW4_", StringComparison.Ordinal)) }));
            return Process.Start(start)!;
        }
        async Task FinishWorker(Process process, Task<string> stdout, Task<string> stderr, string phase)
        {
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            string output = await stdout + await stderr;
            await File.WriteAllTextAsync(input + "." + phase + ".log", output);
            process.ExitCode.ShouldBe(0, output);
            XElement result = XDocument.Load(input + "." + phase + ".xml").Root!.Element("assembly")!;
            result.Attribute("passed")!.Value.ShouldBe("1"); result.Attribute("skipped")!.Value.ShouldBe("0");
        }
    }

    [Fact]
    public async Task NearLimitLegacyOriginalRecoversWithLargerBoundedPhysicalProof()
    {
        string tenant = "review4-large-" + Guid.NewGuid().ToString("N");
        using DaprClient client = Client();
        var store = new DaprReadModelStore(client);
        var scope = new SharedProjectionScope("statestore", tenant, "widget", "legacy-large", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "legacy", "inventory", new Dictionary<string, long>());
        var state = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
        string scratch = tenant + ":private";
        var batch = new ReadModelBatch(new(scope.StoreName, tenant, scope.Domain, scope.Family, scope.Family, "legacy-large"),
            [ReadModelBatchOperation.Write(scratch, new JsonObject { ["payload"] = new string('x', 750_000) }, ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(scope.StateKey, state.Value!, ReadModelBatchConcurrency.Match(state.ETag!))]);
        int manifestBytes = ReadModelBatchFingerprint.BuildCanonicalManifest(batch).Length;
        manifestBytes.ShouldBeGreaterThan(990_000); manifestBytes.ShouldBeLessThanOrEqualTo(1_048_576);
        var raw = new DaprReadModelBatchStateAccessor(client, scope.StoreName);
        await Should.ThrowAsync<InvalidOperationException>(() => new ReadModelBatchProtocol(raw, new(), NullLogger.Instance)
            .ExecuteAsync(batch, new RetainedBatchLiveFaultInjector(ReadModelBatchPhase.BeforeCommit, -1), default));
        (await ReadModelPendingBatchRecovery.RecoverAsync(new InterruptedLegacyScratchLiveAccessor(raw, scratch, false), new(),
            NullLogger.Instance, batch.Scope, scratch, null, default)).IsResolved.ShouldBeFalse();
        string proofKey = ReadModelBatchKeys.ScratchCompensationKey(batch.Scope.ComputeScopeHash());
        RawStateEntry proof = await raw.ReadAsync(proofKey, default);
        proof.Value.Length.ShouldBeGreaterThan(1_048_576); proof.Value.Length.ShouldBeLessThanOrEqualTo(ReadModelLegacyScratchCompensation.MaxEncodedBytes);
        using DaprClient replacement = Client();
        store = new DaprReadModelStore(replacement);
        (await store.RecoverPendingAsync(batch.Scope, scratch)).IsResolved.ShouldBeTrue();
        (await store.RecoverPendingAsync(batch.Scope, scratch)).IsResolved.ShouldBeTrue();
        await new SharedProjectionEpochCoordinator(store, store).AbortAsync(scope, "legacy", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        (await _redis.GetDatabase().KeyExistsAsync(Key(scratch))).ShouldBeFalse();
        (await _redis.GetDatabase().KeyExistsAsync(Key(proofKey))).ShouldBeFalse();
        TestContext.Current.TestOutputHelper!.WriteLine($"review4-near-limit;original-manifest-bytes={manifestBytes};physical-proof-bytes={proof.Value.Length};repeated-replacement-recovery=true;private-and-proof-absent=true;epoch=Open");
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ExpiredInstalledPayloadSettlesAfterProviderDeleteRefusalOrLostResponse(bool retained, bool afterDelete)
    {
        using DaprClient client = Client();
        string tenant = "review4-delete-" + Guid.NewGuid().ToString("N");
        string model = tenant + ":model", checkpoint = tenant + ":checkpoint";
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);
        DateTimeOffset deadline = clock.GetUtcNow().AddMinutes(1);
        var options = new ReadModelBatchOptions { TimeProvider = clock };
        var batch = new ReadModelBatch(new("statestore", tenant, "widget", "owner", "detail", "expiry-delete"),
            [ReadModelBatchOperation.WriteExpiring(model, Value(1), ReadModelBatchConcurrency.LastWrite, deadline),
                ReadModelBatchOperation.Write(checkpoint, Value(1), ReadModelBatchConcurrency.LastWrite)])
            { RetainManifestForRecovery = retained };
        var raw = new DaprReadModelBatchStateAccessor(client, "statestore");
        IReadModelBatchStateAccessor interrupted = afterDelete ? new InterruptedLegacyScratchLiveAccessor(raw, model, true)
            : new RefusedExpiryDeleteLiveAccessor(raw, model);
        var crossing = new RetainedDeadlineLiveFaultInjector(clock, deadline);
        (await new ReadModelBatchProtocol(interrupted, options, NullLogger.Instance).ExecuteAsync(batch, crossing, default)).IsSuccess.ShouldBe(afterDelete,
            "a lost delete response may complete only when physical read-back proves absence");
        RawStateEntry pending = await raw.ReadAsync(model, default);
        pending.Exists.ShouldBe(!afterDelete);
        if (pending.Exists)
        {
            ReadModelBatchEnvelope.IsEnvelope(pending.Value.Span).ShouldBeTrue();
            (await _redis.GetDatabase().KeyTimeToLiveAsync(Key(model))).ShouldBeNull("the pending own envelope has no physical retention and cannot be Completed");
        }
        var store = new DaprReadModelStore(client, Microsoft.Extensions.Options.Options.Create(options));
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        (await store.ExecuteAsync(batch)).IsSuccess.ShouldBeTrue();
        (await _redis.GetDatabase().KeyExistsAsync(Key(model))).ShouldBeFalse();
        (await _redis.GetDatabase().KeyExistsAsync(Key(checkpoint))).ShouldBeTrue();
        TestContext.Current.TestOutputHelper!.WriteLine($"review4-expiry-delete;retained={retained};lost-delete-response={afterDelete};pending-unretained-own-envelope={!afterDelete};model-physically-absent=true;checkpoint-retained=true;repeated-settlement=true");
    }

    [Fact]
    public async Task NamedAbortReclaimsLateAcceptedOwnerAfterCleanupFlagsAndAllowsOrdinaryDeliveryAndFreshNamedRebuild()
    {
        string tenant = "review4-named-late-" + Guid.NewGuid().ToString("N");
        using DaprClient client = Client();
        var store = new DaprReadModelStore(client);
        var handler = new LivePartitionedScratchHandler();
        SharedProjectionScope scope = handler.CreateScope(tenant);
        var identity = NamedIdentity(tenant, "late-accepted-owner");
        string session = Hexalith.EventStore.DomainService.DomainSharedProjectionRebuildSessionKey.Compute(identity);
        string scratch = Hexalith.EventStore.DomainService.DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(session, 0, 0);
        var capturing = new CapturingScratchLiveBatchStore(client, store, scratch);
        var coordinator = new SharedProjectionEpochCoordinator(store, capturing);
        using var original = NamedProvider(store, capturing, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var first = NamedEvent(1);
        (await NamedBegin(original, identity, [("owner-a", first)])).Status.ShouldBe(Hexalith.EventStore.Contracts.Projections.ProjectionDispatchStatus.Completed);
        (await NamedAccumulate(original, identity, "owner-a", first)).Status.ShouldBe(Hexalith.EventStore.Contracts.Projections.ProjectionDispatchStatus.Completed);
        var abort = new Hexalith.EventStore.DomainService.DomainSharedProjectionRebuildRequest(
            Hexalith.EventStore.DomainService.DomainSharedProjectionRebuildProtocol.Version,
            Hexalith.EventStore.DomainService.DomainSharedProjectionRebuildAction.Abort, identity);
        (await NamedDispatch(original, abort)).Status.ShouldBe(Hexalith.EventStore.Contracts.Projections.ProjectionDispatchStatus.Completed);
        var terminal = (await store.GetAsync<Hexalith.EventStore.DomainService.DomainSharedProjectionRebuildSessionState>("statestore", session)).Value!;
        terminal.ScratchIndexesCleaned.ShouldBeTrue(); terminal.ScratchCleanedOwnerCount.ShouldBe(1);
        byte[] opened = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.StateKey), "data"))!;
        var raw = new DaprReadModelBatchStateAccessor(client, "statestore");
        (await raw.TryWriteAsync(scratch, capturing.OriginalEnvelope, string.Empty, default)).ShouldBeTrue();
        using DaprClient replacement = Client();
        store = new DaprReadModelStore(replacement); coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var resumed = NamedProvider(store, store, coordinator, handler);
        (await NamedDispatch(resumed, abort)).Status.ShouldBe(Hexalith.EventStore.Contracts.Projections.ProjectionDispatchStatus.AlreadyCompleted);
        (await _redis.GetDatabase().KeyExistsAsync(Key(scratch))).ShouldBeFalse();
        ((byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.StateKey), "data"))!).ShouldBe(opened);
        var growth = NamedEvent(9) with { GlobalPosition = 2, SequenceNumber = 1, MessageId = "after-late-owner" };
        var delivered = await Hexalith.EventStore.DomainService.DomainProjectionDispatcher.DispatchAsync(resumed,
            new Hexalith.EventStore.Contracts.Projections.ProjectionDispatchRequest(
                new Hexalith.EventStore.Contracts.Projections.ProjectionRequest(tenant, "widget", "owner-a", [growth]),
                [handler.ProjectionType], growth.MessageId, identity.CatalogFingerprint),
            new ProjectionDispatchOptions(),
            new Hexalith.EventStore.DomainService.DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, default);
        delivered.Outcomes.Single().Status.ShouldBe(Hexalith.EventStore.Contracts.Projections.ProjectionDispatchStatus.Completed);
        var fresh = NamedIdentity(tenant, "fresh-after-late-owner");
        (await NamedBegin(resumed, fresh, [("owner-a", growth)])).Status.ShouldBe(Hexalith.EventStore.Contracts.Projections.ProjectionDispatchStatus.Completed);
        var accepted = await NamedAccumulate(resumed, fresh, "owner-a", growth);
        (await NamedFinish(resumed, fresh, accepted)).Status.ShouldBe(Hexalith.EventStore.Contracts.Projections.ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.ReadAsync<JsonObject>(scope, "row:owner-a")).Value!["count"]!.GetValue<int>().ShouldBe(9);
        TestContext.Current.TestOutputHelper!.WriteLine("review4-production-named;late-original-private-persisted=true;cleanup-flags-already-terminal=true;repeated-Abort-trigger=true;private-absent=true;exact-Open-preserved=true;ordinary-delivery=true;fresh-named-rebuild=true;no-test-authored-recovery-or-private-delete=true");
    }

}
