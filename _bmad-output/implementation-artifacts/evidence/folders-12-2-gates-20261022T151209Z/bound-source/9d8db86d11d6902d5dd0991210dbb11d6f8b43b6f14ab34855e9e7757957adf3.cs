
using System.Net;
using System.Text;

using Hexalith.EventStore.HealthChecks;

using Microsoft.Extensions.Configuration;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.HealthChecks;

public class DaprActorPlacementProbeTests {
    // Mirrors the real DAPR 1.17 /v1.0/metadata shape (actorRuntime section).
    private const string ConnectedMetadata =
        """{"id":"eventstore","actorRuntime":{"runtimeStatus":"RUNNING","hostReady":true,"placement":"placement: connected","activeActors":[{"type":"ProjectionActor","count":1}]}}""";

    private const string DisconnectedMetadata =
        """{"id":"eventstore","actorRuntime":{"runtimeStatus":"RUNNING","hostReady":false,"placement":"placement: disconnected"}}""";

    private const string NoActorRuntimeMetadata =
        """{"id":"eventstore","components":[]}""";

    private static IConfiguration EmptyConfiguration() => Substitute.For<IConfiguration>();

    private static DaprActorPlacementProbe CreateProbe(HttpStatusCode status, string json) {
        var httpClient = new HttpClient(new StubHandler(status, json));
        return new DaprActorPlacementProbe(httpClient, EmptyConfiguration());
    }

    [Fact]
    public async Task CheckAsync_HostReady_ParsesConnectedPlacement() {
        DaprActorPlacementProbe probe = CreateProbe(HttpStatusCode.OK, ConnectedMetadata);

        DaprActorPlacementStatus status = await probe.CheckAsync(CancellationToken.None);

        status.MetadataReachable.ShouldBeTrue();
        status.HostReady.ShouldBeTrue();
        status.Placement.ShouldBe("placement: connected");
        status.RuntimeStatus.ShouldBe("RUNNING");
    }

    [Fact]
    public async Task CheckAsync_HostNotReady_ParsesDisconnectedPlacement() {
        DaprActorPlacementProbe probe = CreateProbe(HttpStatusCode.OK, DisconnectedMetadata);

        DaprActorPlacementStatus status = await probe.CheckAsync(CancellationToken.None);

        status.MetadataReachable.ShouldBeTrue();
        status.HostReady.ShouldBeFalse();
        status.Placement.ShouldBe("placement: disconnected");
    }

    [Fact]
    public async Task CheckAsync_NoActorRuntimeSection_ReturnsHostNotReady() {
        DaprActorPlacementProbe probe = CreateProbe(HttpStatusCode.OK, NoActorRuntimeMetadata);

        DaprActorPlacementStatus status = await probe.CheckAsync(CancellationToken.None);

        status.MetadataReachable.ShouldBeTrue();
        status.HostReady.ShouldBeFalse();
        status.Placement.ShouldBeNull();
    }

    [Fact]
    public async Task CheckAsync_SidecarReturnsError_ThrowsHttpRequestException() {
        DaprActorPlacementProbe probe = CreateProbe(HttpStatusCode.ServiceUnavailable, "{}");

        _ = await Should.ThrowAsync<HttpRequestException>(() => probe.CheckAsync(CancellationToken.None));
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() => new DaprActorPlacementProbe(null!, EmptyConfiguration()));

    [Fact]
    public void Constructor_NullConfiguration_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() => new DaprActorPlacementProbe(new HttpClient(), null!));

    private sealed class StubHandler(HttpStatusCode status, string json) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status) {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }
}
