
using System.Runtime.Serialization;
using System.Text.Json;
using System.Xml.Linq;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging;
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
        var expected = QueryResult.FromPayload(payload);

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // Act
        QueryResult result = await actor.QueryAsync(CreateEnvelope());

        // Assert
        result.Success.ShouldBeTrue();
        result.GetPayload().GetProperty("count").GetInt32().ShouldBe(42);
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
        var expected = QueryResult.FromPayload(payload);

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // First call populates cache
        _ = await actor.QueryAsync(CreateEnvelope());

        // Act — second call with same ETag should hit cache
        QueryResult result = await actor.QueryAsync(CreateEnvelope());

        // Assert
        result.Success.ShouldBeTrue();
        result.GetPayload().GetProperty("count").GetInt32().ShouldBe(42);
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

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService,
            QueryResult.FromPayload(payload1), QueryResult.FromPayload(payload2));

        // First call populates cache
        _ = await actor.QueryAsync(CreateEnvelope());

        // Act — second call with different ETag → cache miss → re-query
        QueryResult result = await actor.QueryAsync(CreateEnvelope());

        // Assert
        result.GetPayload().GetProperty("count").GetInt32().ShouldBe(2);
        actor.ExecuteCallCount.ShouldBe(2); // Called twice (cache invalidated)
    }

    [Fact]
    public async Task QueryAsync_NullETag_AlwaysExecutesQuery() {
        // Arrange — cold start, no ETag
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        JsonElement payload = JsonDocument.Parse("{\"count\":1}").RootElement;
        var expected = QueryResult.FromPayload(payload);

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
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
        var expected = QueryResult.FromPayload(payload);

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
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
        var expected = QueryResult.FromPayload(payload);

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // First call populates cache
        QueryResult first = await actor.QueryAsync(CreateEnvelope());

        // Act — second call should return cached data
        QueryResult second = await actor.QueryAsync(CreateEnvelope());

        // Assert — same payload
        first.GetPayload().GetProperty("value").GetInt32().ShouldBe(99);
        second.GetPayload().GetProperty("value").GetInt32().ShouldBe(99);
        actor.ExecuteCallCount.ShouldBe(1); // Cached
    }

    [Fact]
    public async Task QueryAsync_ExecuteQueryFails_DoesNotUpdateCache() {
        // Arrange
        string currentETag = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(currentETag);

        var failedResult = QueryResult.Failure("Not found");

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
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
        var expected = QueryResult.FromPayload(payload);

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
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
        var result1 = QueryResult.FromPayload(payload, "order-list");
        var result2 = QueryResult.FromPayload(payload, "order-list");

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, result1, result2);

        // Act — first call discovers, second call uses discovered type
        _ = await actor.QueryAsync(CreateEnvelope(domain: "orders"));
        _ = await actor.QueryAsync(CreateEnvelope(domain: "orders"));

        // Assert — second call should use "order-list" for ETag lookup
        _ = await eTagService.Received(1).GetCurrentETagAsync("orders", "tenant1", Arg.Any<CancellationToken>());
        _ = await eTagService.Received(1).GetCurrentETagAsync("order-list", "tenant1", Arg.Any<CancellationToken>());
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
        var result1 = QueryResult.FromPayload(payload, "order-list");
        var result2 = QueryResult.FromPayload(payload, "order-list");

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
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
        var result1 = QueryResult.FromPayload(payload1, "order-list");
        var result2 = QueryResult.FromPayload(payload2, "order-list");

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
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
        var expected = QueryResult.FromPayload(payload);

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // Act
        _ = await actor.QueryAsync(CreateEnvelope());
        _ = await actor.QueryAsync(CreateEnvelope());

        // Assert — uses "counter" (envelope.Domain), cache hit on second call
        _ = await eTagService.Received(2).GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>());
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
        var expected = QueryResult.FromPayload(payload, "");

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // Act
        _ = await actor.QueryAsync(CreateEnvelope());
        _ = await actor.QueryAsync(CreateEnvelope());

        // Assert — fallback to envelope.Domain, caching works
        _ = await eTagService.Received(2).GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>());
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
        var expected = QueryResult.FromPayload(payload, "evil:type");

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // Act
        _ = await actor.QueryAsync(CreateEnvelope());
        _ = await actor.QueryAsync(CreateEnvelope());

        // Assert — rejected, falls back to "counter", caching works normally
        _ = await eTagService.Received(2).GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>());
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
        var expected = QueryResult.FromPayload(payload, longProjectionType);

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, expected);

        // Act
        _ = await actor.QueryAsync(CreateEnvelope());
        _ = await actor.QueryAsync(CreateEnvelope());

        // Assert — rejected, falls back to "counter"
        _ = await eTagService.Received(2).GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>());
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
        var result1 = QueryResult.FromPayload(payload, "order-list");
        var result2 = QueryResult.FromPayload(payload, "order-summary"); // different!

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, result1, result2);

        // Act — first call discovers "order-list" (domain=counter, so first call skips cache)
        _ = await actor.QueryAsync(CreateEnvelope(domain: "counter"));
        // Second call uses "order-list" for ETag (discovered), returns "order-summary" (flip-flop)
        _ = await actor.QueryAsync(CreateEnvelope(domain: "counter"));

        // Assert — second call uses "order-list" (first discovery), NOT "order-summary"
        _ = await eTagService.Received(1).GetCurrentETagAsync("order-list", "tenant1", Arg.Any<CancellationToken>());
        // Should NOT have called with "order-summary"
        _ = await eTagService.DidNotReceive().GetCurrentETagAsync("order-summary", "tenant1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_SameProjectionTypeAsDomain_CachesNormally() {
        // Arrange — domain="counter", projection returns "counter" (same)
        string counterETag = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(counterETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":1}").RootElement;
        var expected = QueryResult.FromPayload(payload, "counter");

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
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
        QueryResult original = new(true, null, null, "order-list");

        var serializer = new DataContractSerializer(typeof(QueryResult));
        using var ms = new MemoryStream();
        serializer.WriteObject(ms, original);

        ms.Position = 0;
        string xml = new StreamReader(ms).ReadToEnd();
        var document = XDocument.Parse(xml);
        document.Descendants().Where(e => e.Name.LocalName == "ProjectionType").Remove();
        string oldFormatXml = document.ToString(SaveOptions.DisableFormatting);

        oldFormatXml.ShouldNotContain("ProjectionType");

        using var ms2 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(oldFormatXml));
        var deserialized = (QueryResult?)serializer.ReadObject(ms2);

        // Assert — deserialization succeeds and ProjectionType defaults to null.
        _ = deserialized.ShouldNotBeNull();
        deserialized.Success.ShouldBeTrue();
        deserialized.ProjectionType.ShouldBeNull();
    }

    // ===== Story 9-5: Audit gap-filling tests — log assertion on empty/whitespace ProjectionType =====

    [Fact]
    public async Task QueryAsync_EmptyProjectionType_FallsBackToEnvelopeDomainAndLogsWarning() {
        // Arrange — empty ProjectionType should log InvalidProjectionType warning (EventId 1076)
        string counterETag = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(counterETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":1}").RootElement;
        var expected = QueryResult.FromPayload(payload, "");

        var logEntries = new List<LogEntry>();
        var testLogger = new TestLoggerInstance(logEntries);
        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, testLogger, expected);

        // Act
        _ = await actor.QueryAsync(CreateEnvelope());
        _ = await actor.QueryAsync(CreateEnvelope());

        // Assert — behavioral: fallback to envelope.Domain, caching works
        _ = await eTagService.Received(2).GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>());
        actor.ExecuteCallCount.ShouldBe(1);

        // Assert — observability: warning logged with EventId 1076 and reason "empty or whitespace"
        logEntries.ShouldContain(e =>
            e.Level == LogLevel.Warning
            && e.EventId.Id == 1076
            && e.Message.Contains("empty or whitespace", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task QueryAsync_WhitespaceOnlyProjectionType_FallsBackToEnvelopeDomainAndLogsWarning() {
        // Arrange — whitespace-only ProjectionType treated same as empty
        string counterETag = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(counterETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":1}").RootElement;
        var expected = QueryResult.FromPayload(payload, "   ");

        var logEntries = new List<LogEntry>();
        var testLogger = new TestLoggerInstance(logEntries);
        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, testLogger, expected);

        // Act
        _ = await actor.QueryAsync(CreateEnvelope());
        _ = await actor.QueryAsync(CreateEnvelope());

        // Assert — behavioral: fallback to envelope.Domain, caching works
        _ = await eTagService.Received(2).GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>());
        actor.ExecuteCallCount.ShouldBe(1);

        // Assert — observability: warning logged with EventId 1076 and reason "empty or whitespace"
        logEntries.ShouldContain(e =>
            e.Level == LogLevel.Warning
            && e.EventId.Id == 1076
            && e.Message.Contains("empty or whitespace", StringComparison.OrdinalIgnoreCase));
    }

    // ===== Story 9-4: Gap-filling tests =====

    [Fact]
    public async Task QueryAsync_DeactivationReactivation_FreshInstanceHasNullCachedState() {
        // Arrange — simulate actor deactivation by creating a new instance (DAPR destroys the old one).
        // Use a domain/projection mismatch so we can verify mapping reset + relearn on the fresh instance.
        string domainETag = GenerateTestETag("orders");
        string projectionETag = GenerateTestETag("order-list");
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "tenant1", Arg.Any<CancellationToken>())
            .Returns(domainETag, domainETag);
        _ = eTagService.GetCurrentETagAsync("order-list", "tenant1", Arg.Any<CancellationToken>())
            .Returns(projectionETag, projectionETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":42}").RootElement;
        var expected = QueryResult.FromPayload(payload, "order-list");
        QueryEnvelope envelope = CreateEnvelope(domain: "orders");

        var host1 = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor1 = new TestCachingProjectionActor(host1, eTagService, expected);

        // Warm/discover on first actor instance
        _ = await actor1.QueryAsync(envelope); // learns projection type
        _ = await actor1.QueryAsync(envelope); // now uses learned type
        actor1.ExecuteCallCount.ShouldBe(2);

        // Act — simulate deactivation: create a fresh actor instance (new object = cleared fields)
        var host2 = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor2 = new TestCachingProjectionActor(host2, eTagService, expected);
        QueryResult first = await actor2.QueryAsync(envelope); // should fallback to domain again
        QueryResult second = await actor2.QueryAsync(envelope); // should use re-learned projection type

        // Assert — fresh instance starts with null mapping and re-learns projection type
        actor2.ExecuteCallCount.ShouldBe(2);
        first.Success.ShouldBeTrue();
        second.Success.ShouldBeTrue();
        first.GetPayload().GetProperty("count").GetInt32().ShouldBe(42);
        second.GetPayload().GetProperty("count").GetInt32().ShouldBe(42);
        _ = await eTagService.Received(2).GetCurrentETagAsync("orders", "tenant1", Arg.Any<CancellationToken>());
        _ = await eTagService.Received(2).GetCurrentETagAsync("order-list", "tenant1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_FullCacheLifecycle_MissHitInvalidationMiss() {
        // Arrange — full cycle: cold miss → warm hit → ETag changes → cache miss (re-query)
        string etag1 = GenerateTestETag();
        string etag2 = GenerateTestETag();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(etag1, etag1, etag2, etag2);

        JsonElement payload1 = JsonDocument.Parse("{\"count\":1}").RootElement;
        JsonElement payload2 = JsonDocument.Parse("{\"count\":2}").RootElement;

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService,
            QueryResult.FromPayload(payload1), QueryResult.FromPayload(payload2));

        // Act 1: Cold miss — first call, nothing cached
        QueryResult miss1 = await actor.QueryAsync(CreateEnvelope());
        miss1.GetPayload().GetProperty("count").GetInt32().ShouldBe(1);
        actor.ExecuteCallCount.ShouldBe(1);

        // Act 2: Warm hit — same ETag, cached payload returned
        QueryResult hit = await actor.QueryAsync(CreateEnvelope());
        hit.GetPayload().GetProperty("count").GetInt32().ShouldBe(1); // cached data
        actor.ExecuteCallCount.ShouldBe(1); // NOT called again

        // Act 3: ETag changes (projection updated) — cache invalidation, re-query
        QueryResult miss2 = await actor.QueryAsync(CreateEnvelope());
        miss2.GetPayload().GetProperty("count").GetInt32().ShouldBe(2); // fresh data
        actor.ExecuteCallCount.ShouldBe(2); // called again on cache miss

        // Act 4: Same refreshed ETag — cached fresh data should now be reused
        QueryResult hit2 = await actor.QueryAsync(CreateEnvelope());

        // Assert — cache is re-warmed after invalidation miss
        hit2.GetPayload().GetProperty("count").GetInt32().ShouldBe(2);
        actor.ExecuteCallCount.ShouldBe(2); // still 2 = no extra microservice call
    }

    [Fact]
    public async Task QueryAsync_InvalidThenValidProjectionType_DiscoverySucceedsOnSecondCall() {
        // Arrange — first response has invalid ProjectionType (contains colon),
        // second response has valid ProjectionType. Force cache miss between calls
        // by changing ETag so second ExecuteQueryAsync fires.
        string counterETag1 = GenerateTestETag();
        string counterETag2 = GenerateTestETag();
        string validETag = GenerateTestETag("valid-type");
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("counter", "tenant1", Arg.Any<CancellationToken>())
            .Returns(counterETag1, counterETag2); // different ETags → cache miss on second call
        _ = eTagService.GetCurrentETagAsync("valid-type", "tenant1", Arg.Any<CancellationToken>())
            .Returns(validETag);

        JsonElement payload = JsonDocument.Parse("{\"count\":1}").RootElement;
        var result1 = QueryResult.FromPayload(payload, "evil:type"); // invalid (colon)
        var result2 = QueryResult.FromPayload(payload, "valid-type"); // valid

        var host = ActorHost.CreateForTest<TestCachingProjectionActor>();
        var actor = new TestCachingProjectionActor(host, eTagService, result1, result2);

        // Act — first call: invalid projection type rejected, no discovery, caches normally
        _ = await actor.QueryAsync(CreateEnvelope());
        actor.ExecuteCallCount.ShouldBe(1);

        // Second call: ETag changed → cache miss → ExecuteQueryAsync returns valid type → discovered
        // Discovery mismatch (valid-type != counter) → first call with discovery skips cache
        _ = await actor.QueryAsync(CreateEnvelope());
        actor.ExecuteCallCount.ShouldBe(2);

        // Assert — third call uses discovered "valid-type" for ETag lookup
        _ = await actor.QueryAsync(CreateEnvelope());
        _ = await eTagService.Received(1).GetCurrentETagAsync("valid-type", "tenant1", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Concrete test implementation of <see cref="CachingProjectionActor"/>.
    /// Returns preconfigured results in sequence.
    /// </summary>
    private sealed class TestCachingProjectionActor : CachingProjectionActor {
        private readonly QueryResult[] _results;

        public TestCachingProjectionActor(
            ActorHost host,
            IETagService eTagService,
            params QueryResult[] results)
            : base(host, eTagService, NullLogger<TestCachingProjectionActor>.Instance) => _results = results;

        public TestCachingProjectionActor(
            ActorHost host,
            IETagService eTagService,
            ILogger logger,
            params QueryResult[] results)
            : base(host, eTagService, logger) => _results = results;

        public int ExecuteCallCount { get; private set; }

        protected override Task<QueryResult> ExecuteQueryAsync(QueryEnvelope envelope) {
            int index = Math.Min(ExecuteCallCount, _results.Length - 1);
            ExecuteCallCount++;
            return Task.FromResult(_results[index]);
        }
    }

    private sealed class TestLoggerInstance(List<LogEntry> entries) : ILogger {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
    }

    private record LogEntry(LogLevel Level, EventId EventId, string Message);
}
