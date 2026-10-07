
extern alias eventstore;

using System.Net;
using System.Net.Http.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.Testing.Fakes;

using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.EventStore.Server.Tests.Integration;

/// <summary>
/// Tier 2 integration tests for ETag actor notification paths.
/// Uses WebApplicationFactory with mocked IActorProxyFactory.
/// </summary>
public class ETagActorIntegrationTests : IClassFixture<ETagActorIntegrationTests.ETagTestFactory>, IDisposable {
    private readonly ETagTestFactory _factory;
    private readonly HttpClient _client;

    private const string ChannelToken = "story-5-5-etag-channel-token";

    public ETagActorIntegrationTests(ETagTestFactory factory) {
        _factory = factory;
        _factory.ResetActors();
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, ChannelToken);
    }

    public void Dispose() {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task CrossProcessPath_ValidNotification_InvokesRegenerateAndReturns200() {
        // Arrange
        var notification = new ProjectionChangedNotification("order-list", "acme");

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/projections/changed", Signed(notification));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _factory.FakeETagActor.RegenerateCount.ShouldBe(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task CrossProcessPath_WithEntityId_InvokesRegenerateAndReturns200() {
        // Arrange
        var notification = new ProjectionChangedNotification("order-list", "acme", "order-123");

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/projections/changed", Signed(notification));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _factory.FakeETagActor.RegenerateCount.ShouldBe(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task CrossProcessPath_WithDetail_RegeneratesBeforeDetailBroadcast() {
        // Arrange
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["freshness"] = "changed",
        };
        var notification = new ProjectionChangedNotification(
            "order-list",
            "acme",
            GroupScope: "order-123",
            Metadata: metadata);
        _factory.Broadcaster
            .When(x => x.BroadcastChangedAsync(Arg.Any<ProjectionChangedDetail>(), Arg.Any<CancellationToken>()))
            .Do(_ => _factory.FakeETagActor.RegenerateCount.ShouldBe(1));

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/projections/changed", Signed(notification)).ConfigureAwait(true);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _factory.FakeETagActor.RegenerateCount.ShouldBe(1);
        await _factory.Broadcaster.Received(1).BroadcastChangedAsync(
            Arg.Is<ProjectionChangedDetail>(d =>
                d.ProjectionType == "order-list"
                && d.TenantId == "acme"
                && d.GroupScope == "order-123"
                && d.Metadata.Count == 1
                && d.Metadata["freshness"] == "changed"),
            Arg.Any<CancellationToken>()).ConfigureAwait(true);
        await _factory.Broadcaster.DidNotReceiveWithAnyArgs()
            .BroadcastChangedAsync(default!, default!, default).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task CrossProcessPath_WithTenantWideEmptyDetail_BroadcastsDetail() {
        // Arrange
        var notification = new ProjectionChangedNotification(
            "order-list",
            "acme",
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal));

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/projections/changed", Signed(notification)).ConfigureAwait(true);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _factory.FakeETagActor.RegenerateCount.ShouldBe(1);
        await _factory.Broadcaster.Received(1).BroadcastChangedAsync(
            Arg.Is<ProjectionChangedDetail>(d =>
                d.ProjectionType == "order-list"
                && d.TenantId == "acme"
                && d.GroupScope == null
                && d.Metadata.Count == 0),
            Arg.Any<CancellationToken>()).ConfigureAwait(true);
        await _factory.Broadcaster.DidNotReceiveWithAnyArgs()
            .BroadcastChangedAsync(default!, default!, default).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task CrossProcessPath_DetailInvalidScope_ReturnsBadRequestWithoutActorOrBroadcast() {
        // Arrange
        var notification = new ProjectionChangedNotification(
            "order-list",
            "acme",
            GroupScope: "order:123",
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal) {
                ["freshness"] = "changed",
            });

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/projections/changed", Signed(notification)).ConfigureAwait(true);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        _factory.FakeETagActor.RegenerateCount.ShouldBe(0);
        await _factory.Broadcaster.DidNotReceiveWithAnyArgs()
            .BroadcastChangedAsync(default(ProjectionChangedDetail)!, default).ConfigureAwait(true);
        await _factory.Broadcaster.DidNotReceiveWithAnyArgs()
            .BroadcastChangedAsync(default!, default!, default).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task CrossProcessPath_DetailMetadataExceedsEntryLimit_ReturnsBadRequestWithoutActorOrBroadcast() {
        // Arrange
        Dictionary<string, string> metadata = Enumerable
            .Range(0, 17)
            .ToDictionary(i => $"k{i}", i => "v", StringComparer.Ordinal);
        var notification = new ProjectionChangedNotification(
            "order-list",
            "acme",
            GroupScope: "order-123",
            Metadata: metadata);

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/projections/changed", Signed(notification)).ConfigureAwait(true);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        _factory.FakeETagActor.RegenerateCount.ShouldBe(0);
        await _factory.Broadcaster.DidNotReceiveWithAnyArgs()
            .BroadcastChangedAsync(default(ProjectionChangedDetail)!, default).ConfigureAwait(true);
        await _factory.Broadcaster.DidNotReceiveWithAnyArgs()
            .BroadcastChangedAsync(default!, default!, default).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task CrossProcessPath_ActorFailure_ReturnsNon200ForDaprRetry() {
        // Arrange
        _factory.FakeETagActor.ConfiguredException = new InvalidOperationException("actor failure");
        var notification = new ProjectionChangedNotification("order-list", "acme");

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/projections/changed", Signed(notification));

        // Assert — CM-1: non-200 triggers DAPR retry
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task CrossProcessPath_DetailActorFailure_DoesNotBroadcast() {
        // Arrange
        _factory.FakeETagActor.ConfiguredException = new InvalidOperationException("actor failure");
        var notification = new ProjectionChangedNotification(
            "order-list",
            "acme",
            GroupScope: "order-123",
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal) {
                ["freshness"] = "changed",
            });

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/projections/changed", Signed(notification)).ConfigureAwait(true);

        // Assert — CM-1: non-200 triggers DAPR retry and no SignalR broadcast is attempted
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        await _factory.Broadcaster.DidNotReceiveWithAnyArgs()
            .BroadcastChangedAsync(default(ProjectionChangedDetail)!, default).ConfigureAwait(true);
        await _factory.Broadcaster.DidNotReceiveWithAnyArgs()
            .BroadcastChangedAsync(default!, default!, default).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task CrossProcessPath_MissingProjectionType_ReturnsBadRequest() {
        // Arrange
        var notification = new ProjectionChangedNotification("", "acme");

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/projections/changed", Signed(notification));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task CrossProcessPath_MissingTenantId_ReturnsBadRequest() {
        // Arrange
        var notification = new ProjectionChangedNotification("order-list", "");

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/projections/changed", Signed(notification));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task InProcessPath_NotifyProjectionChanged_InvokesRegenerateViaProxy() {
        // Arrange — Use the DaprProjectionChangeNotifier directly from DI
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        Client.Projections.IProjectionChangeNotifier notifier =
            scope.ServiceProvider.GetRequiredService<Client.Projections.IProjectionChangeNotifier>();

        // Act
        await notifier.NotifyProjectionChangedAsync("order-list", "acme");

        // Assert
        _factory.FakeETagActor.RegenerateCount.ShouldBe(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task InProcessPath_WithEntityId_InvokesRegenerate() {
        // Arrange
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        Client.Projections.IProjectionChangeNotifier notifier =
            scope.ServiceProvider.GetRequiredService<Client.Projections.IProjectionChangeNotifier>();

        // Act
        await notifier.NotifyProjectionChangedAsync("order-list", "acme", "order-123");

        // Assert
        _factory.FakeETagActor.RegenerateCount.ShouldBe(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task FakeETagActor_ColdStart_ReturnsNull() {
        // Arrange — fresh actor with no configured ETag
        var actor = new FakeETagActor();

        // Act
        string? result = await actor.GetCurrentETagAsync();

        // Assert — AC #6: cold start returns null
        result.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task FakeETagActor_AfterRegenerate_ReturnsNewETag() {
        // Arrange
        var actor = new FakeETagActor();

        // Act
        string newETag = await actor.RegenerateAsync();
        string? currentETag = await actor.GetCurrentETagAsync();

        // Assert
        _ = currentETag.ShouldNotBeNull();
        currentETag.ShouldBe(newETag);
        newETag.ShouldContain("."); // Self-routing format: {base64url(projectionType)}.{guid}
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task ETagActor_RegenerateAsync_PersistsThenCachesValue() {
        (ETagActor actor, IActorStateManager stateManager) = CreateEtagActor();

        string etag = await actor.RegenerateAsync();

        await stateManager.Received(1).SetStateAsync("etag", etag, Arg.Any<CancellationToken>());
        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
        (await actor.GetCurrentETagAsync()).ShouldBe(etag);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task ETagActor_OnActivateAsync_ColdStart_LoadsNull() {
        (ETagActor actor, IActorStateManager stateManager) = CreateEtagActor();
        _ = stateManager.TryGetStateAsync<string>("etag", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<string>(false, default!));

        await InvokeActivateAsync(actor);

        (await actor.GetCurrentETagAsync()).ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task ETagActor_Reactivate_LoadsPersistedValue() {
        string? persisted = null;
        (ETagActor actor1, IActorStateManager stateManager1) = CreateEtagActor();
        stateManager1.When(x => x.SetStateAsync("etag", Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(call => persisted = call.ArgAt<string>(1));

        string generated = await actor1.RegenerateAsync();

        (ETagActor actor2, IActorStateManager stateManager2) = CreateEtagActor();
        _ = stateManager2.TryGetStateAsync<string>("etag", Arg.Any<CancellationToken>())
            .Returns(_ => new ConditionalValue<string>(true, persisted!));

        await InvokeActivateAsync(actor2);

        (await actor2.GetCurrentETagAsync()).ShouldBe(generated);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task ETagActor_SaveStateFailure_DoesNotUpdateInMemoryCache() {
        (ETagActor actor, IActorStateManager stateManager) = CreateEtagActor();
        _ = stateManager.SaveStateAsync(Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("save failed"));

        _ = await Should.ThrowAsync<InvalidOperationException>(actor.RegenerateAsync);

        (await actor.GetCurrentETagAsync()).ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task ETagActor_OnActivateAsync_OldFormatETag_MigratesToSelfRoutingFormat() {
        string oldFormat = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        (ETagActor actor, IActorStateManager stateManager) = CreateEtagActor();
        _ = stateManager.TryGetStateAsync<string>("etag", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<string>(true, oldFormat));

        await InvokeActivateAsync(actor);

        string? migrated = await actor.GetCurrentETagAsync();

        _ = migrated.ShouldNotBeNull();
        migrated.ShouldContain('.');
        SelfRoutingETag.TryDecode(migrated, out string? projectionType, out _).ShouldBeTrue();
        projectionType.ShouldBe("order-list");
        await stateManager.Received(1).SetStateAsync("etag", Arg.Is<string>(value => value.Contains('.')), Arg.Any<CancellationToken>());
        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task NotificationCausesETagStaleness_OldETagNoLongerMatches() {
        // Arrange — set initial ETag so Gate 1 has something to compare against
        string initialETag = await _factory.FakeETagActor.RegenerateAsync();
        string? before = await _factory.FakeETagActor.GetCurrentETagAsync();
        before.ShouldBe(initialETag);

        // Act — send cross-process notification which regenerates the ETag
        var notification = new ProjectionChangedNotification("test-projection", "acme");
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/projections/changed", Signed(notification));

        // Assert — ETag was regenerated, old ETag is now stale
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _ = _factory.MockProxyFactory.Received().CreateActorProxy<IETagActor>(
            Arg.Is<ActorId>(id => id.GetId() == "test-projection:acme"),
            Arg.Is(ETagActor.ETagActorTypeName));
        string? after = await _factory.FakeETagActor.GetCurrentETagAsync();
        _ = after.ShouldNotBeNull();
        after.ShouldNotBe(initialETag, "ETag should have been regenerated, making the previous one stale");
        _factory.FakeETagActor.RegenerateCount.ShouldBe(2); // 1 initial + 1 from notification
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task ETagActor_OnActivateAsync_OldFormatMigrationFailure_LeavesCacheNull() {
        string oldFormat = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        (ETagActor actor, IActorStateManager stateManager) = CreateEtagActor();
        _ = stateManager.TryGetStateAsync<string>("etag", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<string>(true, oldFormat));
        _ = stateManager.SaveStateAsync(Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("migration failed"));

        await InvokeActivateAsync(actor);

        (await actor.GetCurrentETagAsync()).ShouldBeNull();
    }

    private static (ETagActor Actor, IActorStateManager StateManager) CreateEtagActor() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<ETagActor> logger = Substitute.For<ILogger<ETagActor>>();
        var host = ActorHost.CreateForTest<ETagActor>(
            new ActorTestOptions { ActorId = new ActorId("order-list:acme") });
        var actor = new ETagActor(host, logger);

        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        return (actor, stateManager);
    }

    private static async Task InvokeActivateAsync(ETagActor actor) {
        System.Reflection.MethodInfo method = typeof(ETagActor).GetMethod(
            "OnActivateAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            ?? throw new InvalidOperationException("Could not locate OnActivateAsync.");

        var task = (Task)method.Invoke(actor, null)!;
        await task.ConfigureAwait(false);
    }

    /// <summary>
    /// Story 5.5: a callback without the app-channel token, or whose signed publisher provenance is absent, forged,
    /// stale, unauthorized, unbound, partially bound, or bound to another tenant/topic, performs no actor call,
    /// freshness change, or broadcast. An unbound provenance (an authority's client-credentials token) is a replayable
    /// forgery for any tenant, so it is denied like any other.
    /// </summary>
    /// <param name="scenario">The denial scenario.</param>
    /// <param name="expectedStatus">The expected bounded status.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    [InlineData("absent-provenance", HttpStatusCode.Forbidden)]
    [InlineData("missing-channel-token", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-channel-token", HttpStatusCode.Unauthorized)]
    [InlineData("duplicate-channel-token", HttpStatusCode.Unauthorized)]
    [InlineData("forged-signature", HttpStatusCode.Forbidden)]
    [InlineData("expired", HttpStatusCode.Forbidden)]
    [InlineData("unauthorized-publisher", HttpStatusCode.Forbidden)]
    [InlineData("wrong-operation", HttpStatusCode.Forbidden)]
    [InlineData("wrong-audience", HttpStatusCode.Forbidden)]
    [InlineData("topic-mismatch", HttpStatusCode.Forbidden)]
    [InlineData("tenant-mismatch", HttpStatusCode.Forbidden)]
    [InlineData("projection-mismatch", HttpStatusCode.Forbidden)]
    [InlineData("unbound-provenance", HttpStatusCode.Forbidden)]
    [InlineData("missing-tenant-binding", HttpStatusCode.Forbidden)]
    [InlineData("missing-projection-binding", HttpStatusCode.Forbidden)]
    [InlineData("missing-topic-binding", HttpStatusCode.Forbidden)]
    public async Task CrossProcessPath_UnprovenCallback_LeavesFreshnessUnchanged(string scenario, HttpStatusCode expectedStatus) {
        // Arrange
        string initialETag = await _factory.FakeETagActor.RegenerateAsync();
        int baseline = _factory.FakeETagActor.RegenerateCount;
        var notification = new ProjectionChangedNotification(
            "order-list",
            "acme",
            GroupScope: "order-123",
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal) { ["freshness"] = "changed" });
        Dictionary<string, string> bindings = Bindings("order-list", "acme");
        string? provenance = scenario switch {
            "absent-provenance" => null,
            "forged-signature" => WorkloadAssertionTestTokens.Create(
                ProvenanceAudience, Publisher, [EventStoreWorkloadOperations.ProjectionNotify], bindings: bindings,
                signingKey: Convert.ToBase64String(new byte[48])),
            "expired" => WorkloadAssertionTestTokens.Create(
                ProvenanceAudience, Publisher, [EventStoreWorkloadOperations.ProjectionNotify], bindings: bindings,
                issuedAt: DateTime.UtcNow.AddMinutes(-10)),
            "unauthorized-publisher" => WorkloadAssertionTestTokens.Create(
                ProvenanceAudience, "intruder", [EventStoreWorkloadOperations.ProjectionNotify], bindings: bindings),
            "wrong-operation" => WorkloadAssertionTestTokens.Create(
                ProvenanceAudience, Publisher, [EventStoreWorkloadOperations.TrustedEffect], bindings: bindings),
            "wrong-audience" => WorkloadAssertionTestTokens.Create(
                "sample", Publisher, [EventStoreWorkloadOperations.ProjectionNotify], bindings: bindings),
            "topic-mismatch" => WorkloadAssertionTestTokens.Create(
                ProvenanceAudience, Publisher, [EventStoreWorkloadOperations.ProjectionNotify],
                bindings: new Dictionary<string, string>(bindings, StringComparer.Ordinal) {
                    [EventStoreWorkloadAuthenticationDefaults.TopicBindingClaimType] = "globex.order-list.projection-changed",
                }),
            "tenant-mismatch" => WorkloadAssertionTestTokens.Create(
                ProvenanceAudience, Publisher, [EventStoreWorkloadOperations.ProjectionNotify], bindings: Bindings("order-list", "globex")),
            "projection-mismatch" => WorkloadAssertionTestTokens.Create(
                ProvenanceAudience, Publisher, [EventStoreWorkloadOperations.ProjectionNotify], bindings: Bindings("invoice-list", "acme")),
            "unbound-provenance" => WorkloadAssertionTestTokens.Create(
                ProvenanceAudience, Publisher, [EventStoreWorkloadOperations.ProjectionNotify]),
            "missing-tenant-binding" => WorkloadAssertionTestTokens.Create(
                ProvenanceAudience, Publisher, [EventStoreWorkloadOperations.ProjectionNotify],
                bindings: Without(bindings, EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType)),
            "missing-projection-binding" => WorkloadAssertionTestTokens.Create(
                ProvenanceAudience, Publisher, [EventStoreWorkloadOperations.ProjectionNotify],
                bindings: Without(bindings, EventStoreWorkloadAuthenticationDefaults.ProjectionTypeBindingClaimType)),
            "missing-topic-binding" => WorkloadAssertionTestTokens.Create(
                ProvenanceAudience, Publisher, [EventStoreWorkloadOperations.ProjectionNotify],
                bindings: Without(bindings, EventStoreWorkloadAuthenticationDefaults.TopicBindingClaimType)),
            _ => ValidProvenance("order-list", "acme"),
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/projections/changed") {
            Content = JsonContent.Create(notification with { Provenance = provenance }),
        };
        switch (scenario) {
            case "missing-channel-token":
                using (HttpClient bare = _factory.CreateClient()) {
                    HttpResponseMessage bareResponse = await bare.SendAsync(request).ConfigureAwait(true);
                    bareResponse.StatusCode.ShouldBe(expectedStatus, scenario);
                }

                await AssertFreshnessUnchangedAsync(initialETag, baseline).ConfigureAwait(true);
                return;
            case "wrong-channel-token":
                using (HttpClient wrong = _factory.CreateClient()) {
                    wrong.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "wrong-channel-token");
                    HttpResponseMessage wrongResponse = await wrong.SendAsync(request).ConfigureAwait(true);
                    wrongResponse.StatusCode.ShouldBe(expectedStatus, scenario);
                }

                await AssertFreshnessUnchangedAsync(initialETag, baseline).ConfigureAwait(true);
                return;
            case "duplicate-channel-token":
                using (HttpClient duplicate = _factory.CreateClient()) {
                    request.Headers.Add(DaprAppChannelToken.HeaderName, [ChannelToken, ChannelToken]);
                    HttpResponseMessage duplicateResponse = await duplicate.SendAsync(request).ConfigureAwait(true);
                    duplicateResponse.StatusCode.ShouldBe(expectedStatus, scenario);
                }

                await AssertFreshnessUnchangedAsync(initialETag, baseline).ConfigureAwait(true);
                return;
        }

        // Act
        HttpResponseMessage response = await _client.SendAsync(request).ConfigureAwait(true);

        // Assert
        response.StatusCode.ShouldBe(expectedStatus, scenario);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
        body.ShouldNotContain("acme", Case.Sensitive, scenario);
        if (provenance is not null) {
            body.ShouldNotContain(provenance, Case.Sensitive, scenario);
        }

        await AssertFreshnessUnchangedAsync(initialETag, baseline).ConfigureAwait(true);
    }

    /// <summary>
    /// Story 5.5 (P-10): a real publisher → receiver round trip for both notifier overloads. The real
    /// <see cref="DaprProjectionChangeNotifier"/> signs provenance with the host's own trusted issuer, the exact
    /// published notification is delivered to <c>/projections/changed</c>, and only then do ETag regeneration and the
    /// bounded broadcast run. Replaying the same provenance for another tenant changes nothing.
    /// </summary>
    /// <param name="detail">Whether the detail overload publishes the notification.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrossProcessPath_RealPublisherProvenance_RoundTripsThroughTheReceiver(bool detail) {
        // Arrange — a pub/sub publisher whose provenance comes from the host's own trusted issuer.
        IWorkloadAssertionIssuer issuer = _factory.Services.GetRequiredService<IWorkloadAssertionIssuer>();
        issuer.CanBindResources.ShouldBeTrue();
        Dapr.Client.DaprClient daprClient = Substitute.For<Dapr.Client.DaprClient>();
        string? publishedTopic = null;
        ProjectionChangedNotification? published = null;
        _ = daprClient.PublishEventAsync(
                Arg.Any<string>(),
                Arg.Do<string>(topic => publishedTopic = topic),
                Arg.Do<ProjectionChangedNotification>(notification => published = notification),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var publisher = new Server.Projections.DaprProjectionChangeNotifier(
            daprClient,
            Substitute.For<IActorProxyFactory>(),
            Substitute.For<IProjectionChangedBroadcaster>(),
            Options.Create(new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.PubSub }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Server.Projections.DaprProjectionChangeNotifier>.Instance,
            issuer);
        if (detail) {
            await publisher.NotifyProjectionChangedAsync(
                new ProjectionChangedDetail(
                    "order-list",
                    "acme",
                    "order-123",
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["freshness"] = "changed" }),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        else {
            await publisher.NotifyProjectionChangedAsync("order-list", "acme", "order-123", TestContext.Current.CancellationToken).ConfigureAwait(true);
        }

        published.ShouldNotBeNull();
        published.Provenance.ShouldNotBeNullOrWhiteSpace();
        publishedTopic.ShouldBe("acme.order-list.projection-changed");
        string initialETag = await _factory.FakeETagActor.RegenerateAsync().ConfigureAwait(true);
        int baseline = _factory.FakeETagActor.RegenerateCount;

        // Act — deliver exactly what the publisher put on the topic, then replay its provenance for another tenant.
        HttpResponseMessage delivered = await _client.PostAsJsonAsync("/projections/changed", published).ConfigureAwait(true);
        int afterDelivery = _factory.FakeETagActor.RegenerateCount;
        HttpResponseMessage replayed = await _client.PostAsJsonAsync(
            "/projections/changed",
            published with { TenantId = "globex" }).ConfigureAwait(true);

        // Assert
        delivered.StatusCode.ShouldBe(HttpStatusCode.OK);
        afterDelivery.ShouldBe(baseline + 1);
        (await _factory.FakeETagActor.GetCurrentETagAsync().ConfigureAwait(true)).ShouldNotBe(initialETag);
        if (detail) {
            await _factory.Broadcaster.Received(1).BroadcastChangedAsync(
                Arg.Is<ProjectionChangedDetail>(d => d.ProjectionType == "order-list" && d.TenantId == "acme" && d.GroupScope == "order-123"),
                Arg.Any<CancellationToken>()).ConfigureAwait(true);
        }
        else {
            await _factory.Broadcaster.Received(1).BroadcastChangedAsync("order-list", "acme", Arg.Any<CancellationToken>()).ConfigureAwait(true);
        }

        replayed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        _factory.FakeETagActor.RegenerateCount.ShouldBe(afterDelivery);
    }

    private const string Publisher = "eventstore";

    private const string ProvenanceAudience = "eventstore";

    private async Task AssertFreshnessUnchangedAsync(string initialETag, int baseline) {
        _factory.FakeETagActor.RegenerateCount.ShouldBe(baseline);
        (await _factory.FakeETagActor.GetCurrentETagAsync().ConfigureAwait(true)).ShouldBe(initialETag);
        _factory.MockProxyFactory.ReceivedCalls().ShouldBeEmpty();
        _factory.Broadcaster.ReceivedCalls().ShouldBeEmpty();
    }

    private static ProjectionChangedNotification Signed(ProjectionChangedNotification notification)
        => notification with { Provenance = ValidProvenance(notification.ProjectionType, notification.TenantId) };

    private static string ValidProvenance(string projectionType, string tenantId)
        => WorkloadAssertionTestTokens.Create(
            ProvenanceAudience,
            Publisher,
            [EventStoreWorkloadOperations.ProjectionNotify],
            bindings: string.IsNullOrEmpty(projectionType) || string.IsNullOrEmpty(tenantId) ? null : Bindings(projectionType, tenantId));

    private static Dictionary<string, string> Bindings(string projectionType, string tenantId)
        => new(StringComparer.Ordinal) {
            [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = tenantId,
            [EventStoreWorkloadAuthenticationDefaults.ProjectionTypeBindingClaimType] = projectionType,
            [EventStoreWorkloadAuthenticationDefaults.TopicBindingClaimType] = $"{tenantId}.{projectionType}.projection-changed",
        };

    private static Dictionary<string, string> Without(Dictionary<string, string> bindings, string claimType) {
        var remaining = new Dictionary<string, string>(bindings, StringComparer.Ordinal);
        _ = remaining.Remove(claimType);
        return remaining;
    }

    /// <summary>
    /// WebApplicationFactory for ETag actor integration tests.
    /// </summary>
    public class ETagTestFactory : WebApplicationFactory<EventStoreProgram> {
        public FakeETagActor FakeETagActor { get; } = new();
        public IActorProxyFactory MockProxyFactory { get; } = Substitute.For<IActorProxyFactory>();
        public IProjectionChangedBroadcaster Broadcaster { get; } = Substitute.For<IProjectionChangedBroadcaster>();

        public void ResetActors() {
            FakeETagActor.Reset();
            Broadcaster.ClearReceivedCalls();
            MockProxyFactory.ClearReceivedCalls();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            ArgumentNullException.ThrowIfNull(builder);
            _ = builder.UseEnvironment("Development");
            _ = builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> {
                    [DaprAppChannelToken.ConfigurationKey] = ChannelToken,
                }));

            _ = builder.ConfigureTestServices(services => {
                WebApplicationFactoryServiceOverrides.RemoveAdminOperationalIndexHostedService(services);

                // Remove existing registrations and replace with mocks.
                services.RemoveAll<IActorProxyFactory>();
                services.RemoveAll<IProjectionChangedBroadcaster>();

                // Configure mock to return fake ETag actor for any actor ID
                _ = MockProxyFactory
                    .CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), Arg.Is(ETagActor.ETagActorTypeName))
                    .Returns(FakeETagActor);

                _ = services.AddSingleton<IOptions<ProjectionChangeNotifierOptions>>(
                    Options.Create(
                        new ProjectionChangeNotifierOptions {
                            Transport = ProjectionChangeTransport.Direct,
                        }));
                _ = services.AddSingleton(MockProxyFactory);
                _ = services.AddSingleton(Broadcaster);
            });
        }
    }
}
