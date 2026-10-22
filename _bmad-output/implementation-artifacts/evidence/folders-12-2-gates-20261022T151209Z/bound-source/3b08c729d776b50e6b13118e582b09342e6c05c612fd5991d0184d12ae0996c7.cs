
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>
/// Tier 2 live-sidecar integration tests for <see cref="DaprETagService"/>.
/// <para>
/// Unlike <see cref="ETagActorIntegrationTests"/> — which substitutes <see cref="IActorProxyFactory"/>
/// and therefore never builds a real <see cref="ActorProxy"/> — these tests run against a real
/// <c>daprd</c> sidecar (<see cref="DaprTestContainerFixture"/>) and a real <see cref="ETagActor"/>
/// backed by the Redis actor state store. This is the adapter edge that the mock-based unit and
/// "integration" suites cannot reach.
/// </para>
/// <para>
/// Regression coverage (2026-05-25): the cancellation-token change had
/// <see cref="DaprETagService"/> cast its remoting proxy to <see cref="ActorProxy"/> and call the
/// weakly-typed, non-remoting <c>InvokeMethodAsync&lt;string?&gt;(method, ct)</c>. A remoting-built
/// proxy has a null non-remoting interactor, so that call threw <see cref="NullReferenceException"/>
/// inside <c>Dapr.Actors.Client.ActorProxy.InvokeMethodAsync</c>, which the fail-open catch silently
/// converted to a null ETag on every fetch. Both tests below exercise the production code path
/// against a real proxy and assert the *actual* persisted ETag is returned — they fail against the
/// pre-fix code and pass after it. See sprint-change-proposal-2026-05-25-etag-actor-proxy-nre.md.
/// </para>
/// </summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public class DaprETagServiceLiveSidecarTests
{
    private readonly DaprTestContainerFixture _fixture;

    public DaprETagServiceLiveSidecarTests(DaprTestContainerFixture fixture) => _fixture = fixture;

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task GetCurrentETagAsync_AfterRegenerate_ReturnsPersistedETag_NotFailOpenNull()
    {
        _fixture.ThrowIfHostStopped();

        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        const string projectionType = "counter";
        string tenantId = $"etag-live-{Guid.NewGuid():N}";
        string actorId = $"{projectionType}:{tenantId}";

        // Seed real actor state: RegenerateAsync persists a self-routing ETag to the Redis actor
        // state store (and caches it on the activated actor instance).
        IETagActor seedProxy = actorProxyFactory.CreateActorProxy<IETagActor>(
            new ActorId(actorId), ETagActor.ETagActorTypeName);
        string expectedETag = await seedProxy.RegenerateAsync();
        expectedETag.ShouldContain("."); // self-routing format: {base64url(projectionType)}.{guid}

        // Exercise the production service: real remoting proxy creation + remoting invocation over
        // the live sidecar. This is the exact code path that NRE'd before the fix.
        var service = new DaprETagService(
            actorProxyFactory, NullLogger<DaprETagService>.Instance, requestTimeout: TimeSpan.FromSeconds(30));
        string? actual = await service.GetCurrentETagAsync(projectionType, tenantId);

        // End-state assertion (R2-A6): the service returns the ETag the actor actually persisted,
        // not a fail-open null produced by a swallowed NullReferenceException.
        _ = actual.ShouldNotBeNull(
            "DaprETagService must return the ETag persisted by the live ETagActor, not a fail-open null");
        actual.ShouldBe(expectedETag);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task GetCurrentETagAsync_ColdActor_ReturnsNull_WithoutThrowing()
    {
        _fixture.ThrowIfHostStopped();

        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        // A never-regenerated actor: a clean cold start must yield null via the real remoting path —
        // distinguishing a genuine "no ETag yet" null from the pre-fix fail-open null caused by the NRE.
        string tenantId = $"etag-cold-{Guid.NewGuid():N}";
        var service = new DaprETagService(
            actorProxyFactory, NullLogger<DaprETagService>.Instance, requestTimeout: TimeSpan.FromSeconds(30));

        string? actual = await service.GetCurrentETagAsync("counter", tenantId);

        actual.ShouldBeNull();
    }
}
