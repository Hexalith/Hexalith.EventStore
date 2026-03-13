
using System.Text.Json;

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
