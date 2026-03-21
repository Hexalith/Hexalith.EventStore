#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprHealthQueryServiceTests {
    private static DaprHealthQueryService CreateService(
        DaprClient? daprClient = null,
        AdminServerOptions? serverOptions = null) {
        daprClient ??= Substitute.For<DaprClient>();
        serverOptions ??= new AdminServerOptions {
            TraceUrl = "https://traces",
            MetricsUrl = "https://metrics",
            LogsUrl = "https://logs",
        };

        IOptions<AdminServerOptions> options = Options.Create(serverOptions);

        return new DaprHealthQueryService(
            daprClient,
            options,
            new NullAdminAuthContext(),
            NullLogger<DaprHealthQueryService>.Instance);
    }

    private static DaprMetadata CreateMetadata(params DaprComponentsMetadata[] components) => new(
        id: "test-app",
        actors: [],
        extended: new Dictionary<string, string>(),
        components: components);

    [Fact]
    public async Task GetSystemHealthAsync_ReturnsHealthy_WhenAllProbesSucceed() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", []));

        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);
        daprClient.GetStateAsync<string>(
            "statestore", "admin:health-check",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (string?)null);
        daprClient.InvokeMethodAsync<string>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => (string?)"ok");

        DaprHealthQueryService service = CreateService(daprClient);

        SystemHealthReport result = await service.GetSystemHealthAsync();

        result.OverallStatus.ShouldBe(HealthStatus.Healthy);
        result.DaprComponents.Count.ShouldBe(1);
        result.DaprComponents[0].ComponentName.ShouldBe("statestore");
        result.ObservabilityLinks.TraceUrl.ShouldBe("https://traces");
    }

    [Fact]
    public async Task GetSystemHealthAsync_ReturnsDegraded_WhenCommandApiUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(CreateMetadata());
        daprClient.GetStateAsync<string>(
            "statestore", "admin:health-check",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (string?)null);
        daprClient.InvokeMethodAsync<string>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("CommandApi down"));

        DaprHealthQueryService service = CreateService(daprClient);

        SystemHealthReport result = await service.GetSystemHealthAsync();

        result.OverallStatus.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public async Task GetSystemHealthAsync_ReturnsUnhealthy_WhenSidecarUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Sidecar down"));

        DaprHealthQueryService service = CreateService(daprClient);

        SystemHealthReport result = await service.GetSystemHealthAsync();

        result.OverallStatus.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task GetDaprComponentStatusAsync_ReturnsComponents() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = CreateMetadata(
            new DaprComponentsMetadata("statestore", "state.redis", "v1", []),
            new DaprComponentsMetadata("pubsub", "pubsub.redis", "v1", []));

        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        DaprHealthQueryService service = CreateService(daprClient);

        IReadOnlyList<DaprComponentHealth> result = await service.GetDaprComponentStatusAsync();

        result.Count.ShouldBe(2);
        result[0].ComponentName.ShouldBe("statestore");
        result[1].ComponentName.ShouldBe("pubsub");
    }

    [Fact]
    public async Task GetDaprComponentStatusAsync_ReturnsEmpty_WhenSidecarUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Sidecar down"));

        DaprHealthQueryService service = CreateService(daprClient);

        IReadOnlyList<DaprComponentHealth> result = await service.GetDaprComponentStatusAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSystemHealthAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns<DaprMetadata>(_ => throw new OperationCanceledException());

        DaprHealthQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetSystemHealthAsync(cts.Token));
    }

    [Fact]
    public async Task GetSystemHealthAsync_IncludesObservabilityLinks() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(CreateMetadata());

        var opts = new AdminServerOptions {
            TraceUrl = "https://traces.example.com",
            MetricsUrl = "https://metrics.example.com",
            LogsUrl = null,
        };

        DaprHealthQueryService service = CreateService(daprClient, opts);

        SystemHealthReport result = await service.GetSystemHealthAsync();

        result.ObservabilityLinks.TraceUrl.ShouldBe("https://traces.example.com");
        result.ObservabilityLinks.MetricsUrl.ShouldBe("https://metrics.example.com");
        result.ObservabilityLinks.LogsUrl.ShouldBeNull();
    }
}
