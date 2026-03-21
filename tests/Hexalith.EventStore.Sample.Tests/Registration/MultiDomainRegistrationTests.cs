using System.Text.Json;

using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Greeting;
using Hexalith.EventStore.Sample.Greeting.Commands;
using Hexalith.EventStore.Sample.Greeting.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Sample.Tests.Registration;

/// <summary>
/// Tests verifying FR24 multi-domain support: both Counter and Greeting aggregates
/// are discovered, registered, and activated from a single assembly scan.
/// </summary>
public sealed class MultiDomainRegistrationTests {

    // ── Task 2.2: Discovery finds exactly 2 aggregates ──

    [Fact]
    public void AddEventStore_SampleAssembly_DiscoversBothCounterAndGreetingDomains() {
        ServiceCollection services = new();
        _ = services.AddEventStore(typeof(CounterAggregate).Assembly);
        using ServiceProvider provider = services.BuildServiceProvider();

        DiscoveryResult discovery = provider.GetRequiredService<DiscoveryResult>();

        Assert.Equal(2, discovery.Aggregates.Count);
        Assert.Contains(discovery.Aggregates, a => a.DomainName == "counter");
        Assert.Contains(discovery.Aggregates, a => a.DomainName == "greeting");
    }

    // ── Task 2.3: Keyed DI resolution for both domains ──

    [Fact]
    public void AddEventStore_SampleAssembly_ResolvesBothDomainsViaKeyedDI() {
        ServiceCollection services = new();
        _ = services.AddEventStore(typeof(CounterAggregate).Assembly);
        using ServiceProvider provider = services.BuildServiceProvider();

        IDomainProcessor counter = provider.GetRequiredKeyedService<IDomainProcessor>("counter");
        IDomainProcessor greeting = provider.GetRequiredKeyedService<IDomainProcessor>("greeting");

        _ = Assert.IsType<CounterAggregate>(counter);
        _ = Assert.IsType<GreetingAggregate>(greeting);
    }

    // ── Task 2.4: UseEventStore activates both domains with correct resource names ──

    [Fact]
    public void UseEventStore_SampleAssembly_ActivatesBothDomainsWithConventionDerivedResourceNames() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => s.AddEventStore(typeof(CounterAggregate).Assembly))
            .Build();

        _ = host.UseEventStore();

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        Assert.Equal(2, context.Activations.Count);

        EventStoreDomainActivation counterActivation = Assert.Single(context.Activations, a => a.DomainName == "counter");
        Assert.Equal("counter-eventstore", counterActivation.StateStoreName);
        Assert.Equal("counter.events", counterActivation.TopicPattern);

        EventStoreDomainActivation greetingActivation = Assert.Single(context.Activations, a => a.DomainName == "greeting");
        Assert.Equal("greeting-eventstore", greetingActivation.StateStoreName);
        Assert.Equal("greeting.events", greetingActivation.TopicPattern);
    }

    [Fact]
    public async Task ProcessAsync_RequestForCounterDomain_UsesCounterProcessorRegistration() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => s.AddEventStore(typeof(CounterAggregate).Assembly))
            .Build();

        _ = host.UseEventStore();

        using IServiceScope scope = host.Services.CreateScope();
        DomainServiceRequest request = CreateRequest("counter", new IncrementCounter());

        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(scope.ServiceProvider, request);

        DomainServiceWireEvent @event = Assert.Single(result.Events);
        Assert.Equal(typeof(CounterIncremented).FullName, @event.EventTypeName);
    }

    [Fact]
    public async Task ProcessAsync_RequestForGreetingDomain_UsesGreetingProcessorRegistration() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => s.AddEventStore(typeof(CounterAggregate).Assembly))
            .Build();

        _ = host.UseEventStore();

        using IServiceScope scope = host.Services.CreateScope();
        DomainServiceRequest request = CreateRequest("greeting", new SendGreeting());

        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(scope.ServiceProvider, request);

        DomainServiceWireEvent @event = Assert.Single(result.Events);
        Assert.Equal(typeof(GreetingSent).FullName, @event.EventTypeName);
    }

    private static DomainServiceRequest CreateRequest<T>(string domain, T command)
        where T : notnull
        => new(
            new CommandEnvelope(
                MessageId: Guid.NewGuid().ToString(),
                TenantId: "sample-tenant",
                Domain: domain,
                AggregateId: $"{domain}-1",
                CommandType: typeof(T).Name,
                Payload: JsonSerializer.SerializeToUtf8Bytes(command),
                CorrelationId: "corr-1",
                CausationId: null,
                UserId: "test-user",
                Extensions: null),
            CurrentState: null);
}
