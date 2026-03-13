
using System.Runtime.Serialization;
using System.Text.Json;
using System.Xml.Linq;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class CachingProjectionActorTests {
    private static string GenerateTestETag(string projectionType = "counter") =>
        SelfRoutingETag.GenerateNew(projectionType);

    private static QueryEnvelope CreateEnvelope(string domain = "counter", string tenant = "tenant1") =>
        new(
            tenantId: tenant,
            domain: domain,
            aggregateId: "agg-1",
            queryType: "GetCounter",
            payload: [],
            correlationId: "corr-1",
            userId: "user-1");

    [Fact]
    public async Task QueryAsync_CacheMiss_CallsExecuteQueryAsync() {
        // Arrange
        string currentETag = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(currentETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":42}").RootElement;
        var expected = new QueryResult(true, payload);

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // Act
        QueryResult result = await actor.QueryAsync(CreateEnvelope());

        // Assert
        result.Success.ShouldBeTrue();
        result.Payload.GetProperty("count").GetInt32().ShouldBe(42);
        actor.ExecuteCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task QueryAsync_CacheHit_DoesNotCallExecuteQueryAsync() {
        // Arrange
        string currentETag = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(currentETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":42}").RootElement;
        var expected = new QueryResult(true, payload);

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // First call populates cache
        _ = await actor.QueryAsync(CreateEnvelope());

        // Act — second call with same ETag should hit cache
        QueryResult result = await actor.QueryAsync(CreateEnvelope());

        // Assert
        result.Success.ShouldBeTrue();
        result.Payload.GetProperty("count").GetInt32().ShouldBe(42);
        actor.ExecuteCallCount.ShouldBe(1); // Only called once (first call)
    }

    [Fact]
    public async Task QueryAsync_ETagChanges_RefreshesCache() {
        // Arrange
        string initialETag = GenerateTestETag();
        string refreshedETag = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(initialETag, refreshedETag); // Different ETag on second call

        JsonElement payload1 = JsonDocument.Parse("{\"count\":1}").RootElement;
        JsonElement payload2 = JsonDocument.Parse("{\"count\":2}").RootElement;

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService,
            new QueryResult(true, payload1), new QueryResult(true, payload2));

        // First call populates cache
        _ = await actor.QueryAsync(CreateEnvelope());

        // Act — second call with different ETag → cache miss → re-query
        QueryResult result = await actor.QueryAsync(CreateEnvelope());

        // Assert
        result.Payload.GetProperty("count").GetInt32().ShouldBe(2);
        actor.ExecuteCallCount.ShouldBe(2); // Called twice (cache invalidated)
    }

    [Fact]
    public async Task QueryAsync_NullETag_AlwaysExecutesQuery() {
        // Arrange — cold start, no ETag
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        JsonElement payload = JsonDocument.Parse("{\"count\":1}").RootElement;
        var expected = new QueryResult(true, payload);

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // First call
        _ = await actor.QueryAsync(CreateEnvelope());

        // Act — second call, still null ETag → no caching, always execute
        _ = await actor.QueryAsync(CreateEnvelope());

        // Assert
        actor.ExecuteCallCount.ShouldBe(2);
    }

    [Fact]
    public async Task QueryAsync_ETagServiceFailsReturnsNull_ExecutesQueryWithoutCaching() {
        // Arrange — ETag service returns null (fail-open)
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        JsonElement payload = JsonDocument.Parse("{\"data\":\"test\"}").RootElement;
        var expected = new QueryResult(true, payload);

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // Act
        QueryResult result = await actor.QueryAsync(CreateEnvelope());

        // Assert — query executes normally, no caching
        result.Success.ShouldBeTrue();
        actor.ExecuteCallCount.ShouldBe(1);

        // Second call should still execute (nothing cached with null ETag)
        _ = await actor.QueryAsync(CreateEnvelope());
        actor.ExecuteCallCount.ShouldBe(2);
    }

    [Fact]
    public async Task QueryAsync_CacheStoresCorrectPayloadAndETag() {
        // Arrange
        string stableETag = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(stableETag);

        JsonElement payload = JsonDocument.Parse("{\"value\":99}").RootElement;
        var expected = new QueryResult(true, payload);

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // First call populates cache
        QueryResult first = await actor.QueryAsync(CreateEnvelope());

        // Act — second call should return cached data
        QueryResult second = await actor.QueryAsync(CreateEnvelope());

        // Assert — same payload
        first.Payload.GetProperty("value").GetInt32().ShouldBe(99);
        second.Payload.GetProperty("value").GetInt32().ShouldBe(99);
        actor.ExecuteCallCount.ShouldBe(1); // Cached
    }

    [Fact]
    public async Task QueryAsync_ExecuteQueryFails_DoesNotUpdateCache() {
        // Arrange
        string currentETag = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(currentETag);

        var failedResult = new QueryResult(false, default, "Not found");

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, failedResult);

        // First call — failure, should not cache
        QueryResult first = await actor.QueryAsync(CreateEnvelope());
        first.Success.ShouldBeFalse();

        // Act — second call should still execute (nothing cached)
        _ = await actor.QueryAsync(CreateEnvelope());

        // Assert — called twice because failure was not cached
        actor.ExecuteCallCount.ShouldBe(2);
    }

    [Fact]
    public async Task QueryAsync_SelfRoutingETag_CacheHitUsesSameFullValue() {
        // Arrange
        string currentETag = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(currentETag, currentETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":7}").RootElement;
        var expected = new QueryResult(true, payload);

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // Act
        _ = await actor.QueryAsync(CreateEnvelope());
        QueryResult second = await actor.QueryAsync(CreateEnvelope());

        // Assert
        second.Success.ShouldBeTrue();
        SelfRoutingETag.TryDecode(currentETag, out string? projectionType, out _).ShouldBeTrue();
        projectionType.ShouldBe("counter");
        actor.ExecuteCallCount.ShouldBe(1);
    }

    // ===== Runtime projection type discovery tests (Story 18-8) =====

    [Fact]
    public async Task QueryAsync_RuntimeDiscovery_SecondCallUsesDiscoveredProjectionType() {
        // Arrange — domain="orders", projection returns "order-list"
        string ordersETag = GenerateTestETag("orders");
        string orderListETag = GenerateTestETag("order-list");
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "tenant1", Arg.Any<CancellationToken>())
            .Returns(ordersETag);
        _ = eTagService.GetCurrentETagAsync("order-list", "tenant1", Arg.Any<CancellationToken>())
            .Returns(orderListETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":1}").RootElement;
        var result1 = new QueryResult(true, payload, ProjectionType: "order-list");
        var result2 = new QueryResult(true, payload, ProjectionType: "order-list");

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, result1, result2);

        // Act — first call discovers, second call uses discovered type
        _ = await actor.QueryAsync(CreateEnvelope(domain: "orders"));
        _ = await actor.QueryAsync(CreateEnvelope(domain: "orders"));

        // Assert — second call should use "order-list" for ETag lookup
        await eTagService.Received(1).GetCurrentETagAsync("orders", "tenant1", Arg.Any<CancellationToken>());
        await eTagService.Received(1).GetCurrentETagAsync("order-list", "tenant1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_CacheHit_ReturnsDiscoveredProjectionType() {
        // Arrange — first call discovers, second caches, third is a warm cache hit.
        string ordersETag = GenerateTestETag("orders");
        string orderListETag = GenerateTestETag("order-list");
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "tenant1", Arg.Any<CancellationToken>())
            .Returns(ordersETag);
        _ = eTagService.GetCurrentETagAsync("order-list", "tenant1", Arg.Any<CancellationToken>())
            .Returns(orderListETag, orderListETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":1}").RootElement;
        var result1 = new QueryResult(true, payload, ProjectionType: "order-list");
        var result2 = new QueryResult(true, payload, ProjectionType: "order-list");

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, result1, result2);

        // Act
        _ = await actor.QueryAsync(CreateEnvelope(domain: "orders"));
        _ = await actor.QueryAsync(CreateEnvelope(domain: "orders"));
        QueryResult cached = await actor.QueryAsync(CreateEnvelope(domain: "orders"));

        // Assert
        cached.ProjectionType.ShouldBe("order-list");
        actor.ExecuteCallCount.ShouldBe(2);
    }

    [Fact]
    public async Task QueryAsync_DiscoveryMismatch_FirstCallSkipsCache() {
        // Arrange — domain="orders", projection returns "order-list" (different)
        // First call uses envelope.Domain for ETag → potentially wrong projection.
        // Should NOT cache on first call.
        string ordersETag = GenerateTestETag("orders");
        string orderListETag = GenerateTestETag("order-list");
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "tenant1", Arg.Any<CancellationToken>())
            .Returns(ordersETag);
        _ = eTagService.GetCurrentETagAsync("order-list", "tenant1", Arg.Any<CancellationToken>())
            .Returns(orderListETag);

        JsonElement payload1 = JsonDocument.Parse("{\"count\":1}").RootElement;
        JsonElement payload2 = JsonDocument.Parse("{\"count\":2}").RootElement;
        var result1 = new QueryResult(true, payload1, ProjectionType: "order-list");
        var result2 = new QueryResult(true, payload2, ProjectionType: "order-list");

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, result1, result2);

        // Act
        _ = await actor.QueryAsync(CreateEnvelope(domain: "orders")); // cold call, discovers
        _ = await actor.QueryAsync(CreateEnvelope(domain: "orders")); // second call, correct ETag

        // Assert — both calls execute (first skipped cache due to mismatch)
        actor.ExecuteCallCount.ShouldBe(2);
    }

    [Fact]
    public async Task QueryAsync_NullProjectionType_FallsBackToEnvelopeDomain() {
        // Arrange — no ProjectionType in result → use envelope.Domain
        string counterETag = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(counterETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":1}").RootElement;
        var expected = new QueryResult(true, payload, ProjectionType: null);

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // Act
        _ = await actor.QueryAsync(CreateEnvelope());
        _ = await actor.QueryAsync(CreateEnvelope());

        // Assert — uses "counter" (envelope.Domain), cache hit on second call
        await eTagService.Received(2).GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>());
        actor.ExecuteCallCount.ShouldBe(1); // Cached (no projection type mismatch)
    }

    [Fact]
    public async Task QueryAsync_EmptyProjectionType_FallsBackToEnvelopeDomain() {
        // Arrange — empty ProjectionType treated same as null
        string counterETag = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(counterETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":1}").RootElement;
        var expected = new QueryResult(true, payload, ProjectionType: "");

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // Act
        _ = await actor.QueryAsync(CreateEnvelope());
        _ = await actor.QueryAsync(CreateEnvelope());

        // Assert — fallback to envelope.Domain, caching works
        await eTagService.Received(2).GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>());
        actor.ExecuteCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task QueryAsync_ProjectionTypeWithColon_FallsBackToEnvelopeDomain() {
        // Arrange — colon in ProjectionType is invalid (actor ID separator)
        string counterETag = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(counterETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":1}").RootElement;
        var expected = new QueryResult(true, payload, ProjectionType: "evil:type");

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // Act
        _ = await actor.QueryAsync(CreateEnvelope());
        _ = await actor.QueryAsync(CreateEnvelope());

        // Assert — rejected, falls back to "counter", caching works normally
        await eTagService.Received(2).GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>());
        actor.ExecuteCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task QueryAsync_ProjectionTypeExceeding100Chars_FallsBackToEnvelopeDomain() {
        // Arrange — too-long ProjectionType is rejected
        string counterETag = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(counterETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":1}").RootElement;
        string longProjectionType = new('x', 101);
        var expected = new QueryResult(true, payload, ProjectionType: longProjectionType);

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // Act
        _ = await actor.QueryAsync(CreateEnvelope());
        _ = await actor.QueryAsync(CreateEnvelope());

        // Assert — rejected, falls back to "counter"
        await eTagService.Received(2).GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>());
        actor.ExecuteCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task QueryAsync_FlipFloppingProjectionType_FirstDiscoveryWins() {
        // Arrange — first call returns "order-list", second returns "order-summary"
        // First discovery wins; second value is ignored (logged as warning).
        string orderListETag = GenerateTestETag("order-list");
        IETagService eTagService = Substitute.For<IETagService>();
        // First call uses "counter" (fallback), second+ uses "order-list" (discovered)
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(GenerateTestETag());
        _ = eTagService.GetCurrentETagAsync("order-list", "tenant1", Arg.Any<CancellationToken>())
            .Returns(orderListETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":1}").RootElement;
        var result1 = new QueryResult(true, payload, ProjectionType: "order-list");
        var result2 = new QueryResult(true, payload, ProjectionType: "order-summary"); // different!

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, result1, result2);

        // Act — first call discovers "order-list" (domain=counter, so first call skips cache)
        _ = await actor.QueryAsync(CreateEnvelope(domain: "counter"));
        // Second call uses "order-list" for ETag (discovered), returns "order-summary" (flip-flop)
        _ = await actor.QueryAsync(CreateEnvelope(domain: "counter"));

        // Assert — second call uses "order-list" (first discovery), NOT "order-summary"
        await eTagService.Received(1).GetCurrentETagAsync("order-list", "tenant1", Arg.Any<CancellationToken>());
        // Should NOT have called with "order-summary"
        await eTagService.DidNotReceive().GetCurrentETagAsync("order-summary", "tenant1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_SameProjectionTypeAsDomain_CachesNormally() {
        // Arrange — domain="counter", projection returns "counter" (same)
        string counterETag = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(counterETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":1}").RootElement;
        var expected = new QueryResult(true, payload, ProjectionType: "counter");

        ActorHost host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // Act
        _ = await actor.QueryAsync(CreateEnvelope());
        _ = await actor.QueryAsync(CreateEnvelope());

        // Assert — projection type matches domain, no mismatch, caches normally
        actor.ExecuteCallCount.ShouldBe(1); // Cached on first call
    }

    [Fact]
    public void QueryResult_DataContractBackwardCompat_OldFormatWithoutProjectionType_DeserializesWithNullProjectionType() {
        // Arrange — simulate an old serialized QueryResult without ProjectionType.
        QueryResult original = new(true, default, null, "order-list");

        var serializer = new DataContractSerializer(typeof(QueryResult));
        using var ms = new MemoryStream();
        serializer.WriteObject(ms, original);

        ms.Position = 0;
        string xml = new StreamReader(ms).ReadToEnd();
        XDocument document = XDocument.Parse(xml);
        document.Descendants().Where(e => e.Name.LocalName == "ProjectionType").Remove();
        string oldFormatXml = document.ToString(SaveOptions.DisableFormatting);

        oldFormatXml.ShouldNotContain("ProjectionType");

        using var ms2 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(oldFormatXml));
        var deserialized = (QueryResult?)serializer.ReadObject(ms2);

        // Assert — deserialization succeeds and ProjectionType defaults to null.
        deserialized.ShouldNotBeNull();
        deserialized.Success.ShouldBeTrue();
        deserialized.ProjectionType.ShouldBeNull();
    }

    /// <summary>
    /// Concrete test implementation of <see cref="CachingProjectionActor"/>.
    /// Returns preconfigured results in sequence.
    /// </summary>
    private sealed class TestCachingProjectionActor : CachingProjectionActor {
        private readonly QueryResult[] _results;
        private int _callIndex;

        public TestCachingProjectionActor(
            ActorHost host,
            IETagService eTagService,
            params QueryResult[] results)
            : base(host, eTagService, NullLogger<TestCachingProjectionActor>.Instance) {
            _results = results;
        }

        public int ExecuteCallCount => _callIndex;

        protected override Task<QueryResult> ExecuteQueryAsync(QueryEnvelope envelope) {
            int index = Math.Min(_callIndex, _results.Length - 1);
            _callIndex++;
            return Task.FromResult(_results[index]);
        }
    }
}
