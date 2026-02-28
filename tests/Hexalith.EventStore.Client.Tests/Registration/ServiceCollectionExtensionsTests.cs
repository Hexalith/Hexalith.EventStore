
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Client.Tests.Registration;

public class ServiceCollectionExtensionsTests {
    private sealed class TestState {
        public string Value { get; init; } = "default";
    }

    private sealed class TestEvent : IEventPayload;

    private sealed class TestProcessor : DomainProcessorBase<TestState> {
        protected override Task<DomainResult> HandleAsync(CommandEnvelope command, TestState? currentState) {
            var events = new IEventPayload[] { new TestEvent() };
            return Task.FromResult(DomainResult.Success(events));
        }
    }

    private sealed record TestAggregateCommand(string Value);

    private sealed class AggregateProcessor : EventStoreAggregate<TestState> {
        public static DomainResult Handle(TestAggregateCommand command, TestState? state) =>
            DomainResult.Success(new IEventPayload[] { new TestEvent() });
    }

    [Fact]
    public void AddEventStoreClient_RegistersIDomainProcessor() {
        var services = new ServiceCollection();

        _ = services.AddEventStoreClient<TestProcessor>();

        using ServiceProvider provider = services.BuildServiceProvider();
        IDomainProcessor processor = provider.GetRequiredService<IDomainProcessor>();

        Assert.NotNull(processor);
        _ = Assert.IsType<TestProcessor>(processor);
    }

    [Fact]
    public void AddEventStoreClient_ReturnsSameServiceCollection() {
        var services = new ServiceCollection();

        IServiceCollection result = services.AddEventStoreClient<TestProcessor>();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddEventStoreClient_RegistersWithScopedLifetime() {
        var services = new ServiceCollection();
        _ = services.AddEventStoreClient<TestProcessor>();

        using ServiceProvider provider = services.BuildServiceProvider();

        using IServiceScope scope1 = provider.CreateScope();
        using IServiceScope scope2 = provider.CreateScope();

        IDomainProcessor fromScope1A = scope1.ServiceProvider.GetRequiredService<IDomainProcessor>();
        IDomainProcessor fromScope1B = scope1.ServiceProvider.GetRequiredService<IDomainProcessor>();
        IDomainProcessor fromScope2 = scope2.ServiceProvider.GetRequiredService<IDomainProcessor>();

        Assert.Same(fromScope1A, fromScope1B);
        Assert.NotSame(fromScope1A, fromScope2);
    }

    [Fact]
    public void AddEventStoreClient_WithNullServices_ThrowsArgumentNullException() {
        IServiceCollection services = null!;

        _ = Assert.Throws<ArgumentNullException>(services.AddEventStoreClient<TestProcessor>);
    }

    [Fact]
    public async Task AddEventStoreClient_RegistersEventStoreAggregateImplementation() {
        var services = new ServiceCollection();
        _ = services.AddEventStoreClient<AggregateProcessor>();

        using ServiceProvider provider = services.BuildServiceProvider();
        IDomainProcessor processor = provider.GetRequiredService<IDomainProcessor>();

        Assert.IsType<AggregateProcessor>(processor);

        CommandEnvelope command = new(
            TenantId: "tenant",
            Domain: "test",
            AggregateId: "aggregate-1",
            CommandType: nameof(TestAggregateCommand),
            Payload: System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new TestAggregateCommand("ok")),
            CorrelationId: "corr-1",
            CausationId: null,
            UserId: "user",
            Extensions: null);

        DomainResult result = await processor.ProcessAsync(command, new TestState());
        Assert.True(result.IsSuccess);
    }
}
