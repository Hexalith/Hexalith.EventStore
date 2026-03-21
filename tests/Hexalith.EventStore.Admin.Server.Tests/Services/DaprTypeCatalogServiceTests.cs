#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprTypeCatalogServiceTests
{
    private const string StateStoreName = "statestore";

    private static DaprTypeCatalogService CreateService(DaprClient? daprClient = null)
    {
        daprClient ??= Substitute.For<DaprClient>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions
        {
            StateStoreName = StateStoreName,
        });

        return new DaprTypeCatalogService(
            daprClient,
            options,
            NullLogger<DaprTypeCatalogService>.Instance);
    }

    [Fact]
    public async Task ListEventTypesAsync_ReturnsTypes_WhenIndexExists()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var types = new List<EventTypeInfo>
        {
            new("OrderCreated", "orders", false, 1),
            new("OrderRejected", "orders", true, 1),
        };

        daprClient.GetStateAsync<List<EventTypeInfo>>(
            StateStoreName,
            "admin:type-catalog:events:orders",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => types);

        DaprTypeCatalogService service = CreateService(daprClient);

        IReadOnlyList<EventTypeInfo> result = await service.ListEventTypesAsync("orders");

        result.Count.ShouldBe(2);
        result[0].TypeName.ShouldBe("OrderCreated");
    }

    [Fact]
    public async Task ListEventTypesAsync_ReturnsEmpty_WhenIndexNotFound()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<EventTypeInfo>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<EventTypeInfo>?)null);

        DaprTypeCatalogService service = CreateService(daprClient);

        IReadOnlyList<EventTypeInfo> result = await service.ListEventTypesAsync("orders");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListEventTypesAsync_UsesAllKey_WhenDomainNull()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<EventTypeInfo>>(
            StateStoreName,
            "admin:type-catalog:events:all",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => new List<EventTypeInfo>());

        DaprTypeCatalogService service = CreateService(daprClient);

        IReadOnlyList<EventTypeInfo> result = await service.ListEventTypesAsync(null);

        result.ShouldBeEmpty();
        await daprClient.Received(1).GetStateAsync<List<EventTypeInfo>>(
            StateStoreName,
            "admin:type-catalog:events:all",
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListCommandTypesAsync_ReturnsTypes_WhenIndexExists()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var types = new List<CommandTypeInfo>
        {
            new("CreateOrder", "orders", "OrderAggregate"),
        };

        daprClient.GetStateAsync<List<CommandTypeInfo>>(
            StateStoreName,
            "admin:type-catalog:commands:orders",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => types);

        DaprTypeCatalogService service = CreateService(daprClient);

        IReadOnlyList<CommandTypeInfo> result = await service.ListCommandTypesAsync("orders");

        result.Count.ShouldBe(1);
        result[0].TypeName.ShouldBe("CreateOrder");
    }

    [Fact]
    public async Task ListAggregateTypesAsync_ReturnsTypes_WhenIndexExists()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var types = new List<AggregateTypeInfo>
        {
            new("OrderAggregate", "orders", 3, 2, true),
        };

        daprClient.GetStateAsync<List<AggregateTypeInfo>>(
            StateStoreName,
            "admin:type-catalog:aggregates:orders",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => types);

        DaprTypeCatalogService service = CreateService(daprClient);

        IReadOnlyList<AggregateTypeInfo> result = await service.ListAggregateTypesAsync("orders");

        result.Count.ShouldBe(1);
        result[0].TypeName.ShouldBe("OrderAggregate");
        result[0].HasProjections.ShouldBeTrue();
    }

    [Fact]
    public async Task ListEventTypesAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<EventTypeInfo>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns<List<EventTypeInfo>?>(_ => throw new OperationCanceledException());

        DaprTypeCatalogService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.ListEventTypesAsync("orders", cts.Token));
    }

    [Fact]
    public async Task ListEventTypesAsync_ReturnsEmpty_WhenDaprThrows()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<EventTypeInfo>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns<List<EventTypeInfo>?>(_ => throw new InvalidOperationException("Connection failed"));

        DaprTypeCatalogService service = CreateService(daprClient);

        IReadOnlyList<EventTypeInfo> result = await service.ListEventTypesAsync("orders");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListAggregateTypesAsync_ReturnsEmpty_WhenIndexNotFound()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetStateAsync<List<AggregateTypeInfo>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<AggregateTypeInfo>?)null);

        DaprTypeCatalogService service = CreateService(daprClient);

        IReadOnlyList<AggregateTypeInfo> result = await service.ListAggregateTypesAsync("orders");

        result.ShouldBeEmpty();
    }
}
