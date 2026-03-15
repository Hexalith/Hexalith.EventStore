
using System.Text.Json;

using Hexalith.EventStore.Client.Configuration;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Sample.Tests.Registration;

/// <summary>
/// Integration tests validating the full fluent API registration path:
/// AddEventStore() auto-discovery -> UseEventStore() cascade activation -> IDomainProcessor dispatch.
/// Uses the real Counter sample assembly. Each test builds its own isolated IHost.
/// Cache isolation is not needed here because tests always scan the same assembly (CounterAggregate),
/// and the static caches are keyed by assembly — returning correct cached results.
/// </summary>
public sealed class FluentApiRegistrationIntegrationTests {

    // ── AC1: Discovery result validation ──

    [Fact]
    public void AddEventStore_SampleAssembly_DiscoveryResultContainsExactlyOneCounterAggregate() {
        using IHost host = BuildTestHost();

        DiscoveryResult discovery = host.Services.GetRequiredService<DiscoveryResult>();

        Assert.Equal(1, discovery.TotalCount);
        DiscoveredDomain aggregate = Assert.Single(discovery.Aggregates);
        Assert.Empty(discovery.Projections);
        Assert.Equal("counter", aggregate.DomainName);
        Assert.Equal(typeof(CounterAggregate), aggregate.Type);
        Assert.Equal(typeof(CounterState), aggregate.StateType);
        Assert.Equal(DomainKind.Aggregate, aggregate.Kind);
    }

    // ── AC2: Activation context validation ──

    [Fact]
    public void UseEventStore_SampleAssembly_ActivationContextHasCorrectCounterProperties() {
        using IHost host = BuildTestHost();

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();

        Assert.True(context.IsActivated);
        EventStoreDomainActivation activation = Assert.Single(context.Activations);
        Assert.Equal("counter", activation.DomainName);
        Assert.Equal(DomainKind.Aggregate, activation.Kind);
        Assert.Equal(typeof(CounterAggregate), activation.Type);
        Assert.Equal(typeof(CounterState), activation.StateType);
        Assert.Equal("counter-eventstore", activation.StateStoreName);
        Assert.Equal("counter.events", activation.TopicPattern);
        Assert.Equal("deadletter.counter.events", activation.DeadLetterTopicPattern);
    }

    // ── AC3: Keyed service resolution ──

    [Fact]
    public void UseEventStore_SampleAssembly_KeyedDomainProcessorResolvesCounterAggregate() {
        using IHost host = BuildTestHost();
        using IServiceScope scope = host.Services.CreateScope();

        IDomainProcessor processor = scope.ServiceProvider.GetRequiredKeyedService<IDomainProcessor>("counter");

        _ = Assert.IsType<CounterAggregate>(processor);
    }

    // ── AC4: Non-keyed enumeration ──

    [Fact]
    public void UseEventStore_SampleAssembly_NonKeyedEnumerationIncludesCounterAggregate() {
        using IHost host = BuildTestHost();
        using IServiceScope scope = host.Services.CreateScope();

        IEnumerable<IDomainProcessor> processors = scope.ServiceProvider.GetServices<IDomainProcessor>();

        Assert.Contains(processors, p => p is CounterAggregate);
    }

    // ── AC5: Cascade Layer 5 ConfigureDomain ──

    [Fact]
    public void UseEventStore_Layer5ConfigureDomain_OverridesStateStoreNameOnly() {
        using IHost host = BuildTestHost(options =>
            options.ConfigureDomain("counter", o => o.StateStoreName = "custom-store"));

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        EventStoreDomainActivation activation = Assert.Single(context.Activations);

        Assert.Equal("custom-store", activation.StateStoreName);
        Assert.Equal("counter.events", activation.TopicPattern);
        Assert.Equal("deadletter.counter.events", activation.DeadLetterTopicPattern);
    }

    // ── AC6: Cascade Layer 4 appsettings ──

    [Fact]
    public void UseEventStore_Layer4AppSettings_OverridesTopicPatternOnly() {
        using IHost host = BuildTestHostWithAppSettings(
            new Dictionary<string, string?> {
                ["EventStore:Domains:counter:TopicPattern"] = "override.events",
            });

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        EventStoreDomainActivation activation = Assert.Single(context.Activations);

        Assert.Equal("override.events", activation.TopicPattern);
        Assert.Equal("counter-eventstore", activation.StateStoreName);
        Assert.Equal("deadletter.counter.events", activation.DeadLetterTopicPattern);
    }

    // ── AC7: Command dispatch for all Counter commands ──

    [Fact]
    public async Task ProcessAsync_IncrementCounter_ReturnsSuccessWithCounterIncremented() {
        using IHost host = BuildTestHost();
        using IServiceScope scope = host.Services.CreateScope();
        IDomainProcessor processor = scope.ServiceProvider.GetRequiredKeyedService<IDomainProcessor>("counter");

        DomainResult result = await processor.ProcessAsync(CreateCommand(new IncrementCounter()), currentState: null);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Events);
        _ = Assert.IsType<CounterIncremented>(result.Events[0]);
    }

    [Fact]
    public async Task ProcessAsync_DecrementCounterWithZeroState_ReturnsRejection() {
        using IHost host = BuildTestHost();
        using IServiceScope scope = host.Services.CreateScope();
        IDomainProcessor processor = scope.ServiceProvider.GetRequiredKeyedService<IDomainProcessor>("counter");

        DomainResult result = await processor.ProcessAsync(CreateCommand(new DecrementCounter()), currentState: null);

        Assert.True(result.IsRejection);
        _ = Assert.Single(result.Events);
        _ = Assert.IsType<CounterCannotGoNegative>(result.Events[0]);
    }

    [Fact]
    public async Task ProcessAsync_ResetCounterWithZeroState_ReturnsNoOp() {
        using IHost host = BuildTestHost();
        using IServiceScope scope = host.Services.CreateScope();
        IDomainProcessor processor = scope.ServiceProvider.GetRequiredKeyedService<IDomainProcessor>("counter");

        DomainResult result = await processor.ProcessAsync(CreateCommand(new ResetCounter()), currentState: null);

        Assert.True(result.IsNoOp);
        Assert.Empty(result.Events);
    }

    // ── AC8: Backward compatibility with CounterProcessor (independent host) ──

    [Fact]
    public async Task AddEventStoreClient_LegacyPath_RegistersFunctionalProcessorWithoutDiscovery() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddEventStoreClient<CounterProcessor>())
            .Build();

        // DiscoveryResult should NOT be registered (fluent API was not used)
        DiscoveryResult? discovery = host.Services.GetService<DiscoveryResult>();
        Assert.Null(discovery);

        // Legacy processor should be resolvable and functional
        using IServiceScope scope = host.Services.CreateScope();
        IDomainProcessor processor = scope.ServiceProvider.GetRequiredService<IDomainProcessor>();
        _ = Assert.IsType<CounterProcessor>(processor);

        DomainResult result = await processor.ProcessAsync(CreateCommand(new IncrementCounter()), currentState: null);
        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Events);
        _ = Assert.IsType<CounterIncremented>(result.Events[0]);
    }

    // ── AC10: UseEventStore without AddEventStore throws ──

    [Fact]
    public void UseEventStore_WithoutAddEventStore_ThrowsInvalidOperationException() {
        using IHost host = Host.CreateDefaultBuilder().Build();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(host.UseEventStore);

        Assert.Contains("AddEventStore()", ex.Message);
    }

    // ── AC11: Cascade Layer 2 global suffix override ──

    [Fact]
    public void UseEventStore_Layer2GlobalSuffix_OverridesStateStoreNameRetainsTopicPattern() {
        using IHost host = BuildTestHost(options => options.DefaultStateStoreSuffix = "store");

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        EventStoreDomainActivation activation = Assert.Single(context.Activations);

        Assert.Equal("counter-store", activation.StateStoreName);
        Assert.Equal("counter.events", activation.TopicPattern);
    }

    // ── AC13: Scoped service lifetime verification ──

    [Fact]
    public void UseEventStore_TwoScopes_ProduceDifferentDomainProcessorInstances() {
        using IHost host = BuildTestHost();
        using IServiceScope scope1 = host.Services.CreateScope();
        using IServiceScope scope2 = host.Services.CreateScope();

        IDomainProcessor processor1 = scope1.ServiceProvider.GetRequiredKeyedService<IDomainProcessor>("counter");
        IDomainProcessor processor2 = scope2.ServiceProvider.GetRequiredKeyedService<IDomainProcessor>("counter");

        Assert.NotSame(processor1, processor2);
    }

    // ── Helpers ──

    private static IHost BuildTestHost(Action<EventStoreOptions>? configureOptions = null) {
        IHostBuilder builder = Host.CreateDefaultBuilder();
        _ = builder.ConfigureServices(services => {
            if (configureOptions is not null) {
                _ = services.AddEventStore(configureOptions, typeof(CounterAggregate).Assembly);
            }
            else {
                _ = services.AddEventStore(typeof(CounterAggregate).Assembly);
            }
        });
        IHost host = builder.Build();
        _ = host.UseEventStore();
        return host;
    }

    private static IHost BuildTestHostWithAppSettings(Dictionary<string, string?> appSettings) {
        IHostBuilder builder = Host.CreateDefaultBuilder();
        _ = builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(appSettings));
        _ = builder.ConfigureServices(services => services.AddEventStore(typeof(CounterAggregate).Assembly));
        IHost host = builder.Build();
        _ = host.UseEventStore();
        return host;
    }

    private static CommandEnvelope CreateCommand<T>(T command) where T : notnull
        => new(
            MessageId: Guid.NewGuid().ToString(),
            TenantId: "test-tenant",
            Domain: "counter",
            AggregateId: "counter-1",
            CommandType: typeof(T).Name,
            Payload: JsonSerializer.SerializeToUtf8Bytes(command),
            CorrelationId: "corr-1",
            CausationId: null,
            UserId: "test-user",
            Extensions: null);
}
