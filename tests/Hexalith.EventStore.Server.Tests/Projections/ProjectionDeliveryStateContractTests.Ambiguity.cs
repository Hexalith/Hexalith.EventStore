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
