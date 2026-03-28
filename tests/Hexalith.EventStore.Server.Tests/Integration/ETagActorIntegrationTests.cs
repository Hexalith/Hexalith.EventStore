
extern alias eventstore;

using System.Net;
using System.Net.Http.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
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

    public ETagActorIntegrationTests(ETagTestFactory factory) {
        _factory = factory;
        _factory.ResetActors();
        _client = _factory.CreateClient();
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
            "/projections/changed", notification);

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
            "/projections/changed", notification);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _factory.FakeETagActor.RegenerateCount.ShouldBe(1);
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
            "/projections/changed", notification);

        // Assert — CM-1: non-200 triggers DAPR retry
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task CrossProcessPath_MissingProjectionType_ReturnsBadRequest() {
        // Arrange
        var notification = new ProjectionChangedNotification("", "acme");

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/projections/changed", notification);

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
            "/projections/changed", notification);

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
            "/projections/changed", notification);

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

        typeof(Actor).GetProperty("StateManager")?.SetValue(actor, stateManager);

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
    /// WebApplicationFactory for ETag actor integration tests.
    /// </summary>
    public class ETagTestFactory : WebApplicationFactory<EventStoreProgram> {
        public FakeETagActor FakeETagActor { get; } = new();
        public IActorProxyFactory MockProxyFactory { get; } = Substitute.For<IActorProxyFactory>();

        public void ResetActors() => FakeETagActor.Reset();

        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            ArgumentNullException.ThrowIfNull(builder);
            _ = builder.UseEnvironment("Development");

            _ = builder.ConfigureTestServices(services => {
                // Remove existing IActorProxyFactory and replace with mock
                ServiceDescriptor? existingFactory = services.FirstOrDefault(
                    d => d.ServiceType == typeof(IActorProxyFactory));
                if (existingFactory is not null) {
                    _ = services.Remove(existingFactory);
                }

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
            });
        }
    }
}
