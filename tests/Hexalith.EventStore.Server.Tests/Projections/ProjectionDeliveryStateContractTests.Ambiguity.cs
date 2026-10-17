using System.Text;
using System.Text.Json;

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
    public async Task GuardedBody_WideUnknownPropertiesAvoidPerNameAllocationThroughOwningStore(bool beforeHeader)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(10_000, 10_000);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionDeliveryState state = fixture.Rows[route];
        string json = JsonSerializer.Serialize(state, JsonSerializerOptions.Web);
        string unknown = string.Concat(Enumerable.Repeat("\"u\":0,", 50_000));
        byte[] body = Encoding.UTF8.GetBytes(beforeHeader ? json.Insert(1, unknown) : json.Insert(json.Length - 1, "," + unknown.TrimEnd(',')));
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, body);
        int mutations = fixture.Backend.TrySaveByteOperations.Count;
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        ProjectionDeliveryStateBody.Validate(body, fixture.Backend.JsonSerializerOptions, 256);
        using ProjectionDeliveryCostScope costs = ProjectionDeliveryCostDiagnostics.BeginScope();
        int writes = fixture.Backend.TrySaveByteOperations.Count;
        long before = GC.GetAllocatedBytesForCurrentThread();
        ProjectionDeliveryStateReadResult read = await store.ReadAsync(fixture.Identity, route);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        long bound = 4 * ProjectionDeliveryStateSize.GetLimit(state, 256);
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionGuardWideCumulativeAllocation " + JsonSerializer.Serialize(new
        { beforeHeader, unknownProperties = 50_000, bodyBytes = body.Length, cumulativeAllocatedBytes = allocated, regressionBound = bound,
            peakLiveBytesMeasured = false }));
        read.State.ShouldNotBeNull().LastDeliveredSequence.ShouldBe(10_000);
        allocated.ShouldBeLessThanOrEqualTo(bound);
        costs[ProjectionDeliveryCost.MarkerReads].ShouldBe(2);
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(writes);
        fixture.Backend.ByteStoreValue(key).ShouldBe(body);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardedBody_OversizedUnknownNameIsRejectedWithoutAllocatingItsString(bool escaped)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        byte[] body = Encoding.UTF8.GetBytes("{\"" + (escaped ? "\\u0075" : string.Empty) + new string('u', 5_000_000) + "\":0,\"schemaVersion\":2}");
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, body);
        int mutations = fixture.Backend.TrySaveByteOperations.Count;
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        long before = GC.GetAllocatedBytesForCurrentThread();
        ProjectionDeliveryHistoryValidationException? error = null;
        try { await store.ReadAsync(fixture.Identity, route); }
        catch (ProjectionDeliveryHistoryValidationException failure) { error = failure; }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        error.ShouldNotBeNull();
        error.Message.ShouldBe("delivery_proof_limit_exceeded");
        allocated.ShouldBeLessThan(65_536);
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(mutations);
        fixture.Backend.ByteStoreValue(key).ShouldBe(body);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardedBody_RejectsUnderCapNullDigestFloodBeforeTypedAllocation(bool quotedHead)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(10_000, 10_000);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string head = quotedHead ? "\"10000\"" : "10000";
        if (quotedHead) fixture.Backend.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
        byte[] body = Encoding.UTF8.GetBytes("{\"completedMessageIdDigests\":[" + string.Join(',', Enumerable.Repeat("null", 160_000))
            + "],\"lastDeliveredSequence\":" + head + ",\"schemaVersion\":2}");
        body.Length.ShouldBeLessThan((int)ProjectionDeliveryStateSize.GetLimit(fixture.Rows[route], 256));
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, body);
        int mutations = fixture.Backend.TrySaveByteOperations.Count;
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        // Warm the supported serializer/owning-store metadata independently of
        // this corrupt body; the measured attempt still performs its own reads.
        fixture.Backend.SeedByteStore(key, JsonSerializer.SerializeToUtf8Bytes(fixture.Rows[route], fixture.Backend.JsonSerializerOptions));
        await store.ReadAsync(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, body);
        long before = GC.GetAllocatedBytesForCurrentThread();
        ProjectionDeliveryHistoryValidationException? error = null;
        try { await store.ReadAsync(fixture.Identity, route); }
        catch (ProjectionDeliveryHistoryValidationException failure) { error = failure; }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        error.ShouldNotBeNull();
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionGuardMalformedDigestCumulativeAllocation " + JsonSerializer.Serialize(new
        { quotedHead, digestCount = 160_000, bodyBytes = body.Length, cumulativeAllocatedBytes = allocated, peakLiveBytesMeasured = false }));
        error.Message.ShouldBe("delivery_reconciliation_required");
        allocated.ShouldBeLessThan(65_536);
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(mutations);
        fixture.Backend.ByteStoreValue(key).ShouldBe(body);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("0")]
    [InlineData("\"short\"")]
    [InlineData("\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB=\"")]
    [InlineData("")]
    public async Task GuardedBody_RejectsImpossibleDigestShapeAndCardinality(string contents)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        byte[] body = Encoding.UTF8.GetBytes("{\"schemaVersion\":2,\"completedMessageIdDigests\":[" + contents + "],\"lastDeliveredSequence\":1}");
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, body);
        int mutations = fixture.Backend.TrySaveByteOperations.Count;
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        (await Should.ThrowAsync<ProjectionDeliveryHistoryValidationException>(() => store.ReadAsync(fixture.Identity, route)))
            .Message.ShouldBe("delivery_reconciliation_required");
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(mutations);
    }

    [Theory]
    [InlineData("null", null)]
    [InlineData("\"short\"", "short")]
    [InlineData("\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB=\"", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB=")]
    public async Task Schema1DigestConverter_PreservesHistoricalMalformedValuesForOwningClassification(string encoded, string? expected)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string json = JsonSerializer.Serialize(fixture.Rows[route] with { SchemaVersion = 1, GuardEvidenceVersion = null, GuardEvidence = null }, JsonSerializerOptions.Web);
        string digest = fixture.Rows[route].CompletedMessageIdDigests![0];
        json = json.Replace(JsonSerializer.Serialize(digest, JsonSerializerOptions.Web), encoded, StringComparison.Ordinal);
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, Encoding.UTF8.GetBytes(json));
        int mutations = fixture.Backend.TrySaveByteOperations.Count;
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        using ProjectionDeliveryCostScope costs = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await store.ReadAsync(fixture.Identity, route)).State.ShouldNotBeNull().CompletedMessageIdDigests.ShouldHaveSingleItem().ShouldBe(expected!);
        costs[ProjectionDeliveryCost.MarkerReads].ShouldBe(2);
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(mutations);
    }

    [Fact]
    public async Task GuardedBody_EscapedCanonicalDigestPreservesTheCompleteIdentity()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string digest = fixture.Rows[route].CompletedMessageIdDigests![0];
        string escaped = "\\u" + ((int)digest[0]).ToString("x4", System.Globalization.CultureInfo.InvariantCulture) + digest[1..];
        string json = JsonSerializer.Serialize(fixture.Rows[route], JsonSerializerOptions.Web)
            .Replace(JsonSerializer.Serialize(digest, JsonSerializerOptions.Web), "\"" + escaped + "\"", StringComparison.Ordinal);
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, Encoding.UTF8.GetBytes(json));
        int mutations = fixture.Backend.TrySaveByteOperations.Count;
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        using ProjectionDeliveryCostScope costs = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await store.ReadAsync(fixture.Identity, route)).State.ShouldNotBeNull().CompletedMessageIdDigests.ShouldBe(fixture.Rows[route].CompletedMessageIdDigests);
        costs[ProjectionDeliveryCost.MarkerReads].ShouldBe(2);
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(mutations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardedBody_CachedPolicyNamesPreserveEscapedAndCaseMatching(bool insensitive)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1);
        fixture.Backend.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        fixture.Backend.JsonSerializerOptions.PropertyNameCaseInsensitive = insensitive;
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string json = JsonSerializer.Serialize(fixture.Rows[route], fixture.Backend.JsonSerializerOptions);
        string escaped = insensitive ? "SCHEMA\\u005fVERSION" : "schema\\u005fversion";
        json = json.Replace("schema_version", escaped, StringComparison.Ordinal);
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, Encoding.UTF8.GetBytes(json));
        int mutations = fixture.Backend.TrySaveByteOperations.Count;
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        (await store.ReadAsync(fixture.Identity, route)).State.ShouldNotBeNull().LastDeliveredSequence.ShouldBe(1);
        fixture.Backend.SeedByteStore(key, Encoding.UTF8.GetBytes(json.Insert(1, "\"schema_version\":2,")));
        await Should.ThrowAsync<JsonException>(() => store.ReadAsync(fixture.Identity, route));
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(mutations);
    }

    [Fact]
    public async Task GuardedBody_DeepUnknownObjectsStayWithinProofWorkingBudgetThroughOwningStore()
    {
        const int depth = 50_000;
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(10_000, 10_000);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionDeliveryState state = fixture.Rows[route];
        string json = JsonSerializer.Serialize(state, JsonSerializerOptions.Web);
        byte[] body = Encoding.UTF8.GetBytes(json.Insert(1, "\"future\":" + string.Concat(Enumerable.Repeat("{\"u\":", depth))
            + "0" + new string('}', depth) + ","));
        fixture.Backend.JsonSerializerOptions.MaxDepth = depth + 16;
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, body);
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        using ProjectionDeliveryCostScope costs = ProjectionDeliveryCostDiagnostics.BeginScope();
        int writes = fixture.Backend.TrySaveByteOperations.Count;
        long owningBefore = GC.GetAllocatedBytesForCurrentThread();
        ProjectionDeliveryStateReadResult read = await store.ReadAsync(fixture.Identity, route);
        long owningAllocated = GC.GetAllocatedBytesForCurrentThread() - owningBefore;
        read.State.ShouldNotBeNull().LastDeliveredSequence.ShouldBe(10_000);
        costs[ProjectionDeliveryCost.MarkerReads].ShouldBe(2);
        fixture.Backend.ByteStoreValue(key).ShouldBe(body);
        long before = GC.GetAllocatedBytesForCurrentThread();
        ProjectionDeliveryStateBody.Validate(body, fixture.Backend.JsonSerializerOptions, 256);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        long bound = 4 * ProjectionDeliveryStateSize.GetLimit(state, 256);
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionGuardActualDepthAllocation " + JsonSerializer.Serialize(new
        { depth, bodyBytes = body.Length, allocatedBytes = allocated, owningAllocatedBytes = owningAllocated, proofWorkingByteLimit = bound }));
        allocated.ShouldBeLessThanOrEqualTo(bound);
        owningAllocated.ShouldBeLessThanOrEqualTo(bound);
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardedBody_ConfiguredQuotedIntegersRoundtripTheOwnSerializedLargeHead(bool reservation)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(10_000, reservation ? 9_000 : 10_000);
        if (reservation)
        {
            fixture.Http.RejectDispatch = true;
            (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeFalse();
            fixture.Rows.Values.Single().ActiveReservation.ShouldNotBeNull().EndSequence.ShouldBe(10_000);
        }
        fixture.Backend.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            | System.Text.Json.Serialization.JsonNumberHandling.WriteAsString;
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        int previousWrites = fixture.Backend.TrySaveByteOperations.Count;
        using ProjectionDeliveryCostScope costs = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await store.TrySaveAsync(fixture.Identity, route, fixture.Rows[route], string.Empty)).ShouldBeTrue();
        byte[] body = fixture.Backend.ByteStoreValue(ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route))!;
        body.Length.ShouldBeGreaterThan(16_384);
        Encoding.UTF8.GetString(body).ShouldContain(reservation ? "\"endSequence\":\"10000\"" : "\"lastDeliveredSequence\":\"10000\"");
        ProjectionDeliveryStateReadResult read = await store.ReadAsync(fixture.Identity, route);
        read.State.ShouldNotBeNull().LastDeliveredSequence.ShouldBe(reservation ? 9_000 : 10_000);
        (read.State.ActiveReservation?.EndSequence).ShouldBe(reservation ? (long?)10_000 : null);
        costs[ProjectionDeliveryCost.MarkerReads].ShouldBe(2);
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(previousWrites + 1);
    }

    [Fact]
    public async Task GuardedBody_WriteOnlyQuotedIntegerProfileIsRejectedBeforePersistence()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1);
        fixture.Backend.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.WriteAsString;
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        int writes = fixture.Backend.TrySaveByteOperations.Count;
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        await Should.ThrowAsync<JsonException>(() => store.TrySaveAsync(fixture.Identity, route, fixture.Rows[route], string.Empty));
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Theory]
    [InlineData("9223372036854775808")]
    [InlineData("1.0")]
    [InlineData("1e0")]
    public async Task GuardedBody_QuotedHeadRetainsExactIntegerAndOverflowRejection(string head)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1);
        fixture.Backend.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string json = JsonSerializer.Serialize(fixture.Rows[route], JsonSerializerOptions.Web)
            .Replace("\"lastDeliveredSequence\":1", "\"lastDeliveredSequence\":\"" + head + "\"", StringComparison.Ordinal);
        byte[] body = Encoding.UTF8.GetBytes(json);
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, body);
        int writes = fixture.Backend.TrySaveByteOperations.Count;
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        await Should.ThrowAsync<JsonException>(() => store.ReadAsync(fixture.Identity, route));
        fixture.Backend.ByteStoreValue(key).ShouldBe(body);
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(96)]
    public async Task GuardedBody_LargeSupportedSerializerDepthDoesNotPreallocateUnvisitedContainers(int nestedUnknownDepth)
    {
        const int maxDepth = 1_000_000;
        var profile = new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = maxDepth };
        using Dapr.Client.DaprClient supported = new Dapr.Client.DaprClientBuilder().UseJsonSerializationOptions(profile).Build();
        supported.JsonSerializerOptions.MaxDepth.ShouldBe(maxDepth);
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        byte[] original = JsonSerializer.SerializeToUtf8Bytes(fixture.Rows[route], JsonSerializerOptions.Web);
        byte[] body = nestedUnknownDepth == 0 ? original : Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(original)
            .Insert(1, "\"future\":" + new string('[', nestedUnknownDepth) + "0" + new string(']', nestedUnknownDepth) + ","));
        using var backend = new Hexalith.EventStore.Client.Tests.Projections.RecordingDaprClient { PersistTypedState = true };
        backend.JsonSerializerOptions.MaxDepth = supported.JsonSerializerOptions.MaxDepth;
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        backend.SeedByteStore(key, body);
        var store = new DaprProjectionDeliveryStateStore(backend, Options.Create(new ProjectionOptions()));
        using ProjectionDeliveryCostScope costs = ProjectionDeliveryCostDiagnostics.BeginScope();
        ProjectionDeliveryStateReadResult read = await store.ReadAsync(fixture.Identity, route);
        read.State.ShouldNotBeNull().LastDeliveredSequence.ShouldBe(1);
        costs[ProjectionDeliveryCost.MarkerReads].ShouldBe(2);
        backend.TrySaveByteOperations.ShouldBeEmpty();
        backend.ByteStoreValue(key).ShouldBe(body);

        ProjectionDeliveryStateBody.Validate(original, JsonSerializerOptions.Web, 256);
        long before = GC.GetAllocatedBytesForCurrentThread();
        ProjectionDeliveryStateBody.Validate(body, backend.JsonSerializerOptions, 256);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        long bound = 65_536L + 32L * body.Length;
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionGuardDepthAllocation " + JsonSerializer.Serialize(new
        { maxDepth, nestedUnknownDepth, bodyBytes = body.Length, allocatedBytes = allocated, regressionBound = bound }));
        allocated.ShouldBeLessThanOrEqualTo(bound);
    }

    [Theory]
    [InlineData(20_000)]
    [InlineData(4_194_305)]
    public async Task GuardedBody_RejectsByteBudgetBeforeMaterializingTheMalformedDigestGraph(int padding)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        byte[] body = Encoding.UTF8.GetBytes("{\"schemaVersion\":2,\"lastDeliveredSequence\":0,\"completedMessageIdDigests\":[0],\"padding\":\""
            + new string('x', padding) + "\"}");
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        fixture.Backend.SeedByteStore(key, body);
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        var error = await Should.ThrowAsync<ProjectionDeliveryHistoryValidationException>(() => store.ReadAsync(fixture.Identity, route));
        error.Message.ShouldBe("delivery_proof_limit_exceeded");
        fixture.Backend.ByteStoreValue(key).ShouldBe(body);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("case_root")]
    [InlineData("nested")]
    [InlineData("case_nested")]
    public async Task GuardedBody_RejectsDuplicateEvidenceWithTheActualWebNameMatching(string corruption)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string json = JsonSerializer.Serialize(fixture.Rows[route], JsonSerializerOptions.Web);
        json = corruption switch
        {
            "root" => json.Insert(1, "\"schemaVersion\":1,"),
            "case_root" => json.Insert(1, "\"SchemaVersion\":1,"),
            "nested" => json.Replace("\"selection\":{", "\"selection\":{\"version\":999,", StringComparison.Ordinal),
            _ => json.Replace("\"selection\":{", "\"selection\":{\"Version\":999,", StringComparison.Ordinal),
        };
        byte[] body = Encoding.UTF8.GetBytes(json);
        fixture.Backend.SeedByteStore(ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route), body);
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        await Should.ThrowAsync<JsonException>(() => store.ReadAsync(fixture.Identity, route));
    }
}
