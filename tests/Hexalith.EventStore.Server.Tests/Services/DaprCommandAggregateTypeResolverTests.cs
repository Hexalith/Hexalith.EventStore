using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Services;
using Hexalith.EventStore.Testing.Builders;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Services;

public class DaprCommandAggregateTypeResolverTests {
    [Fact]
    public async Task ResolveAsync_UsesTypeCatalogTargetAggregateTypeAndIgnoresCallerExtension() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<CommandTypeInfo>>(
                "statestore",
                "admin:type-catalog:commands:orders",
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<CommandTypeInfo> {
                new CommandTypeInfo("Samples.Orders.CreateOrder", "orders", "Samples.Orders.OrderAggregate"),
            });
        var resolver = new DaprCommandAggregateTypeResolver(
            daprClient,
            Options.Create(new CommandStatusOptions { StateStoreName = "statestore" }),
            NullLogger<DaprCommandAggregateTypeResolver>.Instance);
        CommandEnvelope command = new CommandEnvelopeBuilder()
            .WithDomain("orders")
            .WithCommandType("CreateOrder")
            .WithExtensions(new Dictionary<string, string> {
                ["aggregateType"] = "SpoofedAggregate",
            })
            .Build();

        string? aggregateType = await resolver.ResolveAsync(command);

        aggregateType.ShouldBe("Samples.Orders.OrderAggregate");
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNullWhenCatalogDoesNotMatchCommandType() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<CommandTypeInfo>>(
                "statestore",
                "admin:type-catalog:commands:orders",
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<CommandTypeInfo> {
                new CommandTypeInfo("Samples.Orders.CancelOrder", "orders", "Samples.Orders.OrderAggregate"),
            });
        var resolver = new DaprCommandAggregateTypeResolver(
            daprClient,
            Options.Create(new CommandStatusOptions { StateStoreName = "statestore" }),
            NullLogger<DaprCommandAggregateTypeResolver>.Instance);
        CommandEnvelope command = new CommandEnvelopeBuilder()
            .WithDomain("orders")
            .WithCommandType("CreateOrder")
            .Build();

        string? aggregateType = await resolver.ResolveAsync(command);

        aggregateType.ShouldBeNull();
    }
}
