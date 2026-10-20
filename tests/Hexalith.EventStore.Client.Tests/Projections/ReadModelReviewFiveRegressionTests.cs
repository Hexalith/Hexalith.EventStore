using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class ReadModelReviewFiveRegressionTests
{
    [Theory]
    [InlineData("store")]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("owner")]
    [InlineData("projection")]
    [InlineData("batch")]
    public async Task TerminalLogicalDiscoveryValidatesCompleteOriginalScopeBeforeAnyAuxiliaryWrite(string component)
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var scope = new ReadModelBatchScope("statestore", "tenant", "domain", "owner", "projection", "original");
        var batch = Batch(scope);
        await StopAtTerminal(client, batch);
        ReadModelBatchMarker marker = ReadModelBatchMarker.ReadProof(client.ByteStoreValue(scope.ComputeMarkerKey()))!;
        marker.RecoveryAnchor = null;
        var expected = component switch
        {
            "store" => scope with { StoreName = "foreign" },
            "tenant" => scope with { TenantId = "foreign" },
            "domain" => scope with { Domain = "foreign" },
            "owner" => scope with { AggregateId = "foreign" },
            "projection" => scope with { ProjectionType = "foreign" },
            _ => scope,
        };
        if (component == "batch") marker.BatchId = "foreign";
        client.SeedByteStore(scope.ComputeMarkerKey(), ReadModelBatchCanonicalJson.Serialize(marker));
        var discovered = expected with { BatchId = marker.BatchId };
        string[] keys = Keys(scope, marker.Fingerprint).Concat(Keys(discovered, marker.Fingerprint)).Distinct().ToArray();
        byte[][] before = keys.Select(client.ByteStoreValue).ToArray();
        (await new DaprReadModelStore(client).RecoverPendingAsync(expected, "first")).IsResolved.ShouldBeFalse();
        for (int index = 0; index < keys.Length; index++) client.ByteStoreValue(keys[index]).ShouldBe(before[index], keys[index]);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task TerminalAnchorRejectsMalformedParticipantConcurrencyBeforeAnyMutation(bool invalidMode, bool retainAnchor)
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var scope = new ReadModelBatchScope("statestore", "tenant", "domain", "owner", "projection", "original");
        await StopAtTerminal(client, Batch(scope));
        ReadModelBatchMarker marker = ReadModelBatchMarker.ReadProof(client.ByteStoreValue(scope.ComputeMarkerKey()))!;
        string proofKey = ReadModelBatchParticipantProof.Key(scope, marker.Fingerprint, marker.RecoveryAnchor!.Ordinal);
        if (!retainAnchor)
        {
            marker.RecoveryAnchor = null;
            client.SeedByteStore(scope.ComputeMarkerKey(), ReadModelBatchCanonicalJson.Serialize(marker));
        }
        var proof = JsonSerializer.Deserialize<ReadModelBatchParticipantProof>(client.ByteStoreValue(proofKey), JsonSerializerOptions.Web)!;
        proof = invalidMode ? proof with { ConcurrencyMode = (ReadModelBatchConcurrencyMode)999 }
            : proof with { ExpectedETagDigest = "malformed" };
        client.SeedByteStore(proofKey, ReadModelBatchCanonicalJson.Serialize(proof));
        string[] keys = Keys(scope, marker.Fingerprint);
        byte[][] before = keys.Select(client.ByteStoreValue).ToArray();
        (await new DaprReadModelStore(client).RecoverPendingAsync(scope, scope.ComputeMarkerKey())).IsResolved.ShouldBeFalse();
        for (int index = 0; index < keys.Length; index++) client.ByteStoreValue(keys[index]).ShouldBe(before[index], keys[index]);
    }

    [Fact]
    public async Task TerminalLegacyCompensationRejectsNoncanonicalPointerWithoutTouchingLateOriginalOrForeignProof()
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var store = new DaprReadModelStore(client);
        var shared = new SharedProjectionScope("statestore", "tenant", "domain", "family", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(shared, "writer");
        _ = await coordinator.BeginAsync(shared, "operation", "inventory", new Dictionary<string, long>());
        var state = await store.GetAsync<SharedProjectionEpochState>(shared.StoreName, shared.StateKey);
        var batch = new ReadModelBatch(new(shared.StoreName, shared.TenantId, shared.Domain, shared.Family, shared.Family, "legacy"),
            [ReadModelBatchOperation.Write("first", new { value = 1 }, ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(shared.StateKey, state.Value!, ReadModelBatchConcurrency.Match(state.ETag!))]);
        await Should.ThrowAsync<InvalidOperationException>(() => new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, shared.StoreName), new(), NullLogger.Instance)
            .ExecuteAsync(batch, new DelegatingBatchFaultInjector((phase, _, _) => phase == ReadModelBatchPhase.BeforeCommit
                ? throw new InvalidOperationException("original stopped") : Task.CompletedTask), default));
        byte[] late = client.ByteStoreValue("first");
        (await store.RecoverPendingAsync(batch.Scope, "first")).IsResolved.ShouldBeTrue();
        ReadModelBatchMarker marker = ReadModelBatchMarker.ReadProof(client.ByteStoreValue(batch.Scope.ComputeMarkerKey()))!;
        marker.ScratchCompensationKey = "foreign-proof";
        client.SeedByteStore(batch.Scope.ComputeMarkerKey(), ReadModelBatchCanonicalJson.Serialize(marker));
        client.SeedByteStore("first", late);
        client.SeedByteStore("foreign-proof", "{}"u8.ToArray());
        string[] keys = Keys(batch.Scope, marker.Fingerprint).Concat([shared.StateKey, "foreign-proof"]).ToArray();
        byte[][] before = keys.Select(client.ByteStoreValue).ToArray();
        (await store.RecoverPendingAsync(batch.Scope, batch.Scope.ComputeMarkerKey())).IsResolved.ShouldBeFalse();
        for (int index = 0; index < keys.Length; index++) client.ByteStoreValue(keys[index]).ShouldBe(before[index], keys[index]);
    }

    [Fact]
    public async Task MaximumEscapedLegacyScopeAndKeyRecoverThroughPublicDiscoveryAndRepeatedSettlement()
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var store = new DaprReadModelStore(client);
        string escaped = "x" + new string('\u0001', 511);
        var shared = new SharedProjectionScope(escaped, escaped, escaped, escaped, ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(shared, "writer");
        _ = await coordinator.BeginAsync(shared, "operation", "inventory", new Dictionary<string, long>());
        var state = await store.GetAsync<SharedProjectionEpochState>(shared.StoreName, shared.StateKey);
        var batch = new ReadModelBatch(new(escaped, escaped, escaped, escaped, escaped, escaped),
            [ReadModelBatchOperation.Write(escaped, new { value = 1 }, ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(shared.StateKey, state.Value!, ReadModelBatchConcurrency.Match(state.ETag!))]);
        await Should.ThrowAsync<InvalidOperationException>(() => new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, shared.StoreName), new(), NullLogger.Instance)
            .ExecuteAsync(batch, new DelegatingBatchFaultInjector((phase, _, _) => phase == ReadModelBatchPhase.BeforeCommit
                ? throw new InvalidOperationException("original stopped") : Task.CompletedTask), default));
        byte[] late = client.ByteStoreValue(escaped);
        (await store.RecoverPendingAsync(batch.Scope, escaped)).IsResolved.ShouldBeTrue();
        string fenceKey = ReadModelBatchTerminalFence.Key(batch.Scope.ComputeScopeHash(), ReadModelBatchFingerprint.Compute(batch));
        byte[] fence = client.ByteStoreValue(fenceKey);
        fence.Length.ShouldBeGreaterThan(55_000);
        fence.Length.ShouldBeLessThanOrEqualTo(ReadModelBatchTerminalFence.MaxLegacyEncodedBytes);
        client.SeedByteStore(escaped, late);
        client.SeedByteStore(fenceKey, new byte[ReadModelBatchTerminalFence.MaxLegacyEncodedBytes + 1]);
        (await store.RecoverPendingAsync(batch.Scope, escaped)).IsResolved.ShouldBeFalse();
        client.ByteStoreValue(escaped).ShouldBe(late);
        client.SeedByteStore(fenceKey, "{}"u8.ToArray());
        (await store.RecoverPendingAsync(batch.Scope, escaped)).IsResolved.ShouldBeFalse();
        client.ByteStoreValue(escaped).ShouldBe(late);
        client.SeedByteStore(fenceKey, fence);
        (await new DaprReadModelStore(client).RecoverPendingAsync(batch.Scope, escaped)).IsResolved.ShouldBeTrue();
        (await store.RecoverPendingAsync(batch.Scope, batch.Scope.ComputeMarkerKey())).IsResolved.ShouldBeTrue();
        client.ByteStoreContains(escaped).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompetingOwnerFenceChangesRetireEachOriginalAttemptAndSameActionEventuallySucceeds(bool legacy)
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var store = new DaprReadModelStore(client);
        var shared = new SharedProjectionScope("statestore", "tenant", "domain", "family", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(shared, "writer");
        _ = await coordinator.BeginAsync(shared, "operation", "inventory", new Dictionary<string, long>());
        var originals = new List<(ReadModelBatchScope Scope, byte[] Envelope)>();
        (ReadModelBatchScope Scope, byte[] Envelope)? legacyOriginal = null;
        if (legacy)
        {
            var epoch = await store.GetAsync<SharedProjectionEpochState>(shared.StoreName, shared.StateKey);
            var old = new ReadModelBatch(new(shared.StoreName, shared.TenantId, shared.Domain, shared.Family, shared.Family, "same-action"),
                [ReadModelBatchOperation.Write("first", new { value = 1 }, ReadModelBatchConcurrency.CreateOnly),
                    ReadModelBatchOperation.Write(shared.StateKey, epoch.Value!, ReadModelBatchConcurrency.Match(epoch.ETag!))]);
            await Should.ThrowAsync<InvalidOperationException>(() => new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, shared.StoreName), new(), NullLogger.Instance)
                .ExecuteAsync(old, new DelegatingBatchFaultInjector((phase, _, _) => phase == ReadModelBatchPhase.BeforeCommit
                    ? throw new InvalidOperationException("legacy original stopped") : Task.CompletedTask), default));
            legacyOriginal = (old.Scope, client.ByteStoreValue("first"));
            (await store.RecoverPendingAsync(old.Scope, "first")).IsResolved.ShouldBeTrue();
            await coordinator.ReclaimScratchReceiptAsync(shared, "same-action");
        }
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            client.AfterTrySaveByteState = (key, bytes) =>
            {
                if (key != "first" || !ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) return;
                ReadModelBatchEnvelope envelope = ReadModelBatchEnvelope.FromBytes(bytes)!;
                var marker = ReadModelBatchMarker.ReadProof(client.ByteStoreValue(ReadModelBatchKeys.MarkerKey(envelope.ScopeHash)))!;
                var original = new ReadModelBatchScope(shared.StoreName, shared.TenantId, shared.Domain, shared.Family, shared.Family, marker.BatchId);
                originals.Add((original, bytes.ToArray()));
                client.AfterTrySaveByteState = null;
                reached.TrySetResult(); release.Task.GetAwaiter().GetResult();
            };
            Task<bool> first = Task.Run(() => coordinator.TryWriteScratchAsync(shared, "operation", "first", new { value = 1 }, "same-action", ReadModelBatchConcurrency.CreateOnly));
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                (await new SharedProjectionEpochCoordinator(store, store).TryWriteScratchAsync(shared, "operation", "competitor-" + attempt,
                    new { value = 2 }, "competitor-" + attempt, ReadModelBatchConcurrency.CreateOnly)).ShouldBeTrue();
            }
            finally { release.TrySetResult(); }
            (await first).ShouldBeFalse();
            client.ByteStoreContains("first").ShouldBeFalse();
        }
        originals.Select(item => item.Scope.BatchId).Distinct().Count().ShouldBe(3);
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        (await coordinator.TryWriteScratchAsync(shared, "operation", "first", new { value = 1 }, "same-action", ReadModelBatchConcurrency.CreateOnly)).ShouldBeTrue();
        (await store.GetAsync<JsonObject>(shared.StoreName, "first")).Value!["value"]!.GetValue<int>().ShouldBe(1);
        await coordinator.ReclaimScratchReceiptAsync(shared, "same-action");
        await coordinator.AbortAsync(shared, "operation", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        byte[] open = client.ByteStoreValue(shared.StateKey);
        var raw = new DaprReadModelBatchStateAccessor(client, shared.StoreName);
        RawStateEntry firstRow = await raw.ReadAsync("first", default);
        (await raw.TryDeleteAsync("first", firstRow.ETag, default)).ShouldBeTrue();
        if (legacyOriginal is { } oldOriginal) originals.Add(oldOriginal);
        foreach (var original in originals)
        {
            client.SeedByteStore("first", original.Envelope);
            (await store.RecoverPendingAsync(original.Scope, "first")).IsResolved.ShouldBeTrue();
            client.ByteStoreContains("first").ShouldBeFalse();
            client.ByteStoreValue(shared.StateKey).ShouldBe(open);
        }
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MaximumEscapedTerminalAnchorRetainsOriginalControlBudgetAndRecoversCompletedOrInterruptedReceipt(bool interrupted)
    {
        using var client = new RecordingDaprClient { PersistTypedState = true };
        var store = new DaprReadModelStore(client);
        string escaped = new('<', ReadModelBatchScope.MaxComponentByteLength);
        var scope = new ReadModelBatchScope(escaped, escaped, escaped, escaped, escaped, escaped);
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(5);
        var batch = new ReadModelBatch(scope, [ReadModelBatchOperation.WriteExpiring(escaped, new { value = 1 }, ReadModelBatchConcurrency.CreateOnly, deadline)])
            { RetainManifestForRecovery = true };
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, scope.StoreName), new(), NullLogger.Instance);
        var result = await protocol.ExecuteAsync(batch, interrupted ? new DelegatingBatchFaultInjector((phase, _, _) => phase == ReadModelBatchPhase.RecoveryReceiptWritten
            ? throw new IOException("terminal receipt stopped") : Task.CompletedTask) : null, default);
        result.IsSuccess.ShouldBe(!interrupted);
        byte[] original = client.ByteStoreValue(scope.ComputeMarkerKey());
        original.Length.ShouldBeGreaterThan(ReadModelBatchMarker.MaxControlBytes);
        original.Length.ShouldBeLessThanOrEqualTo(ReadModelBatchMarker.MaxRecoveryEncodedBytes);
        var inflated = JsonNode.Parse(original)!.AsObject();
        inflated["unknown-control"] = new string('x', ReadModelBatchMarker.MaxControlBytes);
        byte[] invalidControl = ReadModelBatchCanonicalJson.Canonicalize(JsonSerializer.SerializeToUtf8Bytes(inflated));
        invalidControl.Length.ShouldBeLessThanOrEqualTo(ReadModelBatchMarker.MaxRecoveryEncodedBytes);
        client.SeedByteStore(scope.ComputeMarkerKey(), invalidControl);
        byte[] participant = client.ByteStoreValue(escaped);
        (await store.RecoverPendingAsync(scope, scope.ComputeMarkerKey())).IsResolved.ShouldBeFalse();
        client.ByteStoreValue(scope.ComputeMarkerKey()).ShouldBe(invalidControl);
        client.ByteStoreValue(escaped).ShouldBe(participant);
        client.SeedByteStore(scope.ComputeMarkerKey(), new byte[ReadModelBatchMarker.MaxRecoveryEncodedBytes + 1]);
        (await store.RecoverPendingAsync(scope, scope.ComputeMarkerKey())).IsResolved.ShouldBeFalse();
        client.ByteStoreValue(escaped).ShouldBe(participant);
        client.SeedByteStore(scope.ComputeMarkerKey(), original);
        (await new DaprReadModelStore(client).RecoverPendingAsync(scope, scope.ComputeMarkerKey())).IsResolved.ShouldBeTrue();
        (await store.RecoverPendingAsync(scope, scope.ComputeMarkerKey())).IsResolved.ShouldBeTrue();
        ReadModelBatchExpiringValue.Decode(client.ByteStoreValue(escaped)).Deadline.ShouldBe(deadline);
        (await store.GetAsync<JsonObject>(scope.StoreName, escaped)).Value!["value"]!.GetValue<int>().ShouldBe(1);
    }

    private static ReadModelBatch Batch(ReadModelBatchScope scope) => new(scope,
        [ReadModelBatchOperation.Write("first", new { value = 1 }, ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.Write("last", new { value = 2 }, ReadModelBatchConcurrency.CreateOnly)]) { RetainManifestForRecovery = true };

    private static async Task StopAtTerminal(RecordingDaprClient client, ReadModelBatch batch)
    {
        byte[]? original = null;
        client.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (key == "first" && ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) original = bytes.ToArray();
        };
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, batch.Scope.StoreName), new(), NullLogger.Instance);
        (await protocol.ExecuteAsync(batch, new DelegatingBatchFaultInjector((phase, _, _) => phase == ReadModelBatchPhase.RecoveryReceiptWritten
            ? throw new IOException("terminal receipt stopped") : Task.CompletedTask), default)).IsSuccess.ShouldBeFalse();
        client.BeforeTrySaveByteState = null;
        client.SeedByteStore("first", original!);
    }

    private static string[] Keys(ReadModelBatchScope scope, string fingerprint) => ["first", "last", scope.ComputeMarkerKey(),
        ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash()), ReadModelBatchKeys.ScratchCompensationKey(scope.ComputeScopeHash()),
        ReadModelBatchTerminalFence.Key(scope.ComputeScopeHash(), fingerprint), scope.ComputeMarkerKey() + ":retired",
        ReadModelBatchParticipantProof.Key(scope, fingerprint, 0), ReadModelBatchParticipantProof.Key(scope, fingerprint, 1)];
}
