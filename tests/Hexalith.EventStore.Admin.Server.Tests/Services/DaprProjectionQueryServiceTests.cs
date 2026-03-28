#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using System.Net;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprProjectionQueryServiceTests {
    private const string StateStoreName = "statestore";

    private static DaprProjectionQueryService CreateService(DaprClient? daprClient = null) {
        daprClient ??= Substitute.For<DaprClient>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            StateStoreName = StateStoreName,
            EventStoreAppId = "eventstore",
        });

        return new DaprProjectionQueryService(
            daprClient,
            options,
            new NullAdminAuthContext(),
            NullLogger<DaprProjectionQueryService>.Instance);
    }

    [Fact]
    public async Task ListProjectionsAsync_ReturnsProjections_WhenIndexExists() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var projections = new List<ProjectionStatus>
        {
            new("OrderSummary", "tenant1", ProjectionStatusType.Running, 0, 10.5, 0, 100, DateTimeOffset.UtcNow),
            new("ShippingView", "tenant1", ProjectionStatusType.Paused, 50, 0, 1, 50, DateTimeOffset.UtcNow),
        };

        daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            "admin:projections:tenant1",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => projections);

        DaprProjectionQueryService service = CreateService(daprClient);

        IReadOnlyList<ProjectionStatus> result = await service.ListProjectionsAsync("tenant1");

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("OrderSummary");
    }

    [Fact]
    public async Task ListProjectionsAsync_ReturnsEmpty_WhenIndexNotFound() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<ProjectionStatus>?)null);

        DaprProjectionQueryService service = CreateService(daprClient);

        IReadOnlyList<ProjectionStatus> result = await service.ListProjectionsAsync("tenant1");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetProjectionDetailAsync_ReturnsFallback_WhenEventStoreUnavailable() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<ProjectionDetail>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => (ProjectionDetail?)null);

        DaprProjectionQueryService service = CreateService(daprClient);

        ProjectionDetail result = await service.GetProjectionDetailAsync("tenant1", "OrderSummary");

        result.ShouldNotBeNull();
        result.Name.ShouldBe("OrderSummary");
        result.TenantId.ShouldBe("tenant1");
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Message.ShouldContain("not-found");
    }

    [Fact]
    public async Task ListProjectionsAsync_ReturnsEmpty_WhenDaprThrows()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns<List<ProjectionStatus>?>(_ => throw new InvalidOperationException("Connection failed"));

        DaprProjectionQueryService service = CreateService(daprClient);

        IReadOnlyList<ProjectionStatus> result = await service.ListProjectionsAsync("tenant1");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListProjectionsAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<ProjectionStatus>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns<List<ProjectionStatus>?>(_ => throw new OperationCanceledException());

        DaprProjectionQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.ListProjectionsAsync("tenant1", cts.Token));
    }

    [Fact]
    public async Task GetProjectionDetailAsync_ReturnsProjectionDetail_WhenEventStoreSucceeds() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        HttpRequestMessage? capturedRequest = null;
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        authContext.GetToken().Returns("projection-token");
        ProjectionDetail expected = new(
            "OrderSummary",
            "tenant1",
            ProjectionStatusType.Running,
            3,
            12.5,
            0,
            42,
            DateTimeOffset.UtcNow,
            [],
            "{\"mode\":\"live\"}",
            ["OrderCreated"]);

        daprClient.InvokeMethodAsync<ProjectionDetail>(
            Arg.Do<HttpRequestMessage>(request => capturedRequest = request),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprProjectionQueryService service = new(
            daprClient,
            Options.Create(new AdminServerOptions {
                StateStoreName = StateStoreName,
                EventStoreAppId = "eventstore",
            }),
            authContext,
            NullLogger<DaprProjectionQueryService>.Instance);

        ProjectionDetail result = await service.GetProjectionDetailAsync("tenant1", "OrderSummary");

        result.ShouldBe(expected);
        capturedRequest.ShouldNotBeNull();
        capturedRequest!.Method.ShouldBe(HttpMethod.Get);
        capturedRequest.RequestUri!.ToString().ShouldContain("api/v1/admin/projections/tenant1/OrderSummary");
        capturedRequest.Headers.Authorization!.Parameter.ShouldBe("projection-token");
    }
}
