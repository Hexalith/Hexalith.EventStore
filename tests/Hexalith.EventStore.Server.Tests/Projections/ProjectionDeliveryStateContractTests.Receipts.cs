using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public partial class ProjectionDeliveryStateContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardedReceipts_UnderCapObjectFloodFailsBeforeTypedGraphAllocation(bool customProfile)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(10_000, 10_000);
        if (customProfile)
        {
            fixture.Backend.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            fixture.Backend.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString;
        }
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string json = customProfile ? "{\"completed_receipts\":[" : "{\"completedReceipts\":[";
        json += string.Join(',', Enumerable.Repeat("{}", 100_000)) + (customProfile
            ? "],\"last_delivered_sequence\":\"10000\",\"first_retained_sequence\":\"9745\",\"schema_version\":\"2\"}"
            : "],\"lastDeliveredSequence\":10000,\"firstRetainedSequence\":9745,\"schemaVersion\":2}");
        byte[] body = Encoding.UTF8.GetBytes(json);
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        // Warm the owning serializer independently; the corrupt attempt retains its own I/O.
        fixture.Backend.SeedByteStore(key, JsonSerializer.SerializeToUtf8Bytes(fixture.Rows[route], fixture.Backend.JsonSerializerOptions));
        await store.ReadAsync(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, body);
        int writes = fixture.Backend.TrySaveByteOperations.Count;
        long bound = 4 * ProjectionDeliveryStateSize.GetLimit(fixture.Rows[route], 256);
        body.Length.ShouldBeLessThan((int)(bound / 4));
        long before = GC.GetAllocatedBytesForCurrentThread();
        ProjectionDeliveryHistoryValidationException? error = null;
        try { await store.ReadAsync(fixture.Identity, route); }
        catch (ProjectionDeliveryHistoryValidationException failure) { error = failure; }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        error.ShouldNotBeNull().Message.ShouldBe("delivery_reconciliation_required");
        allocated.ShouldBeLessThan(65_536);
        long retained = ProjectionRetainedGraphSize.Measure(error);
        retained.ShouldBeLessThan(bound);
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionGuardReceiptFlood " + JsonSerializer.Serialize(new
        { customProfile, receiptObjects = 100_000, bodyBytes = body.Length, cumulativeAllocatedBytes = allocated,
            retainedExceptionBytes = retained, retainedTypedReceiptGraphBytes = 0, proofWorkingByteLimit = bound, peakLiveBytesMeasured = false }));
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(writes);
        fixture.Backend.ByteStoreValue(key).ShouldBe(body);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("0")]
    [InlineData("[]")]
    [InlineData("{\"sequenceNumber\":1,\"messageId\":null,\"eventFingerprint\":\"f\",\"prefixFingerprint\":\"p\"}")]
    [InlineData("{\"sequenceNumber\":1,\"messageId\":\" \",\"eventFingerprint\":\"f\",\"prefixFingerprint\":\"p\"}")]
    [InlineData("{\"sequenceNumber\":1,\"messageId\":\"id\",\"eventFingerprint\":null,\"prefixFingerprint\":\"p\"}")]
    [InlineData("{\"sequenceNumber\":1,\"messageId\":\"id\",\"eventFingerprint\":\"f\"}")]
    [InlineData("{\"sequenceNumber\":2,\"messageId\":\"id\",\"eventFingerprint\":\"f\",\"prefixFingerprint\":\"p\"}")]
    public async Task GuardedReceipts_InvalidElementFailsTypedBeforeSizing(string receipt)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        var state = JsonNode.Parse(JsonSerializer.Serialize(fixture.Rows[route], JsonSerializerOptions.Web))!.AsObject();
        state["completedReceipts"] = JsonNode.Parse("[" + receipt + "]");
        byte[] body = Encoding.UTF8.GetBytes(state.ToJsonString());
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, body);
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        int writes = fixture.Backend.TrySaveByteOperations.Count;
        (await Should.ThrowAsync<ProjectionDeliveryHistoryValidationException>(() => store.ReadAsync(fixture.Identity, route)))
            .Message.ShouldBe("delivery_reconciliation_required");
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(writes);
        fixture.Backend.ByteStoreValue(key).ShouldBe(body);
    }

    [Theory]
    [InlineData("null", 1)]
    [InlineData("[]", 1)]
    [InlineData("[]", 0)]
    public async Task GuardedReceipts_NullEmptyOrImpossibleRetainedWindowFailsTyped(string receipts, long retained)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        var state = JsonNode.Parse(JsonSerializer.Serialize(fixture.Rows[route], JsonSerializerOptions.Web))!.AsObject();
        state["completedReceipts"] = JsonNode.Parse(receipts);
        state["firstRetainedSequence"] = retained;
        byte[] body = Encoding.UTF8.GetBytes(state.ToJsonString());
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, body);
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        int writes = fixture.Backend.TrySaveByteOperations.Count;
        (await Should.ThrowAsync<ProjectionDeliveryHistoryValidationException>(() => store.ReadAsync(fixture.Identity, route)))
            .Message.ShouldBe("delivery_reconciliation_required");
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Fact]
    public async Task GuardedReceipts_OversizedFingerprintFailsByteCapBeforeAllocatingItsString()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        var state = JsonNode.Parse(JsonSerializer.Serialize(fixture.Rows[route], JsonSerializerOptions.Web))!.AsObject();
        state["completedReceipts"]![0]!["eventFingerprint"] = new string('x', 1_000_000);
        byte[] body = Encoding.UTF8.GetBytes(state.ToJsonString());
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, body);
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        ProjectionDeliveryStateBody.Validate(JsonSerializer.SerializeToUtf8Bytes(fixture.Rows[route], JsonSerializerOptions.Web),
            fixture.Backend.JsonSerializerOptions, 256);
        long before = GC.GetAllocatedBytesForCurrentThread();
        ProjectionDeliveryHistoryValidationException? error = null;
        try { await store.ReadAsync(fixture.Identity, route); }
        catch (ProjectionDeliveryHistoryValidationException failure) { error = failure; }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        error.ShouldNotBeNull().Message.ShouldBe("delivery_proof_limit_exceeded");
        allocated.ShouldBeLessThan(65_536);
        fixture.Backend.ByteStoreValue(key).ShouldBe(body);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(256, false)]
    [InlineData(4096, false)]
    [InlineData(256, true)]
    public async Task GuardedReceipts_ExactRetentionBoundaryPreservesFullProofAndRejectsOneOver(int retention, bool customProfile)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(10_000, 10_000, receiptLimit: retention);
        if (customProfile)
        {
            fixture.Backend.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            fixture.Backend.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString;
        }
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()),
            Options.Create(new ProjectionDeliveryIdempotencyOptions { CompletedReceiptLimit = retention }));
        ProjectionDeliveryState original = fixture.Rows[route];
        fixture.Backend.SeedByteStore(key, JsonSerializer.SerializeToUtf8Bytes(original, fixture.Backend.JsonSerializerOptions));
        using ProjectionDeliveryCostScope costs = ProjectionDeliveryCostDiagnostics.BeginScope();
        ProjectionDeliveryState read = (await store.ReadAsync(fixture.Identity, route)).State.ShouldNotBeNull();
        read.CompletedReceipts.ShouldBe(original.CompletedReceipts);
        read.CompletedMessageIdDigests.ShouldBe(original.CompletedMessageIdDigests);
        long retained = ProjectionRetainedGraphSize.Measure(read);
        long bound = 4 * ProjectionDeliveryStateSize.GetLimit(read, retention);
        retained.ShouldBeLessThanOrEqualTo(bound);
        costs[ProjectionDeliveryCost.MarkerReads].ShouldBe(2);
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionGuardReceiptRetainedGraph " + JsonSerializer.Serialize(new
        { retention, customProfile, retainedTypedProofBytes = retained, proofWorkingByteLimit = bound, cumulativeAllocationsMeasured = false }));
        ProjectionDeliveryReceipt earlier = original.CompletedReceipts![0] with { SequenceNumber = original.FirstRetainedSequence - 1 };
        ProjectionDeliveryState invalid = original with { CompletedReceipts = [earlier, .. original.CompletedReceipts], FirstRetainedSequence = earlier.SequenceNumber };
        fixture.Backend.SeedByteStore(key, JsonSerializer.SerializeToUtf8Bytes(invalid, fixture.Backend.JsonSerializerOptions));
        int writes = fixture.Backend.TrySaveByteOperations.Count;
        (await Should.ThrowAsync<ProjectionDeliveryHistoryValidationException>(() => store.ReadAsync(fixture.Identity, route)))
            .Message.ShouldBe("delivery_reconciliation_required");
        (await Should.ThrowAsync<ProjectionDeliveryHistoryValidationException>(() => store.TrySaveAsync(fixture.Identity, route, invalid, string.Empty)))
            .Message.ShouldBe("delivery_reconciliation_required");
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(writes);
    }
}
