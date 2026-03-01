
using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Tests.Discovery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Client.Tests.Registration;

public class UseEventStoreTests : IDisposable {
    public void Dispose() {
        AssemblyScanner.ClearCache();
        NamingConventionEngine.ClearCache();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void UseEventStore_PopulatesActivationContext_FromDiscoveredDomains() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => s.AddEventStore(typeof(SmokeTestAggregate).Assembly))
            .Build();

        _ = host.UseEventStore();

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        Assert.True(context.IsActivated);
        Assert.NotEmpty(context.Activations);
    }

    [Fact]
    public void UseEventStore_ThrowsInvalidOperationException_WhenAddEventStoreNotCalled() {
        using IHost host = Host.CreateDefaultBuilder().Build();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => host.UseEventStore());
        Assert.Contains("AddEventStore", ex.Message);
    }

    [Fact]
    public void UseEventStore_IdempotentActivation_SecondCallReturnsWithoutError() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => s.AddEventStore(typeof(SmokeTestAggregate).Assembly))
            .Build();

        _ = host.UseEventStore();
        _ = host.UseEventStore(); // second call — should not throw

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        Assert.True(context.IsActivated);
    }

    [Fact]
    public void UseEventStore_NullHost_ThrowsArgumentNullException() {
        IHost host = null!;

        _ = Assert.Throws<ArgumentNullException>(() => host.UseEventStore());
    }

    [Fact]
    public void UseEventStore_ActivationMetadata_HasCorrectConventionDerivedNames() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => s.AddEventStore(typeof(SmokeTestAggregate).Assembly))
            .Build();

        _ = host.UseEventStore();

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        EventStoreDomainActivation aggregate = Assert.Single(context.Activations, a => a.Kind == DomainKind.Aggregate);

        Assert.Equal($"{aggregate.DomainName}-eventstore", aggregate.StateStoreName);
        Assert.Equal($"{aggregate.DomainName}.events", aggregate.TopicPattern);
        Assert.Equal($"deadletter.{aggregate.DomainName}.events", aggregate.DeadLetterTopicPattern);
    }

    [Fact]
    public void UseEventStore_EmptyDiscovery_ProducesEmptyActivationsWithoutError() {
        // Use an assembly with no domain types
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => {
                // Manually register empty DiscoveryResult and context to simulate empty scan
                _ = s.AddSingleton(new DiscoveryResult([], []));
                _ = s.AddSingleton<EventStoreActivationContext>();
            })
            .Build();

        _ = host.UseEventStore();

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        Assert.True(context.IsActivated);
        Assert.Empty(context.Activations);
    }

    [Fact]
    public void UseEventStore_BothAggregatesAndProjections_AppearInActivationsWithCorrectKind() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => s.AddEventStore(typeof(SmokeTestAggregate).Assembly))
            .Build();

        _ = host.UseEventStore();

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        Assert.Contains(context.Activations, a => a.Kind == DomainKind.Aggregate && a.Type == typeof(SmokeTestAggregate));
        Assert.Contains(context.Activations, a => a.Kind == DomainKind.Projection && a.Type == typeof(SmokeTestProjection));
    }

    [Fact]
    public void AddEventStore_BackwardCompat_ServicesWorkWithoutUseEventStore() {
        var services = new ServiceCollection();
        _ = services.AddEventStore(typeof(SmokeTestAggregate).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        DiscoveryResult result = provider.GetRequiredService<DiscoveryResult>();

        Assert.NotNull(result);
        Assert.NotEmpty(result.Aggregates);
    }

    [Fact]
    public void EventStoreActivationContext_Activations_ThrowsBeforeUseEventStoreCalled() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => s.AddEventStore(typeof(SmokeTestAggregate).Assembly))
            .Build();

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();

        _ = Assert.Throws<InvalidOperationException>(() => context.Activations);
    }

    [Fact]
    public void EventStoreActivationContext_IsActivated_FalseBeforeAndTrueAfterUseEventStore() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => s.AddEventStore(typeof(SmokeTestAggregate).Assembly))
            .Build();

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        Assert.False(context.IsActivated);

        _ = host.UseEventStore();

        Assert.True(context.IsActivated);
    }

    [Fact]
    public void AddEventStoreCore_RegistersActivationContext_AsNonActivatedSingleton() {
        var services = new ServiceCollection();
        _ = services.AddEventStore(typeof(SmokeTestAggregate).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        EventStoreActivationContext context = provider.GetRequiredService<EventStoreActivationContext>();

        Assert.NotNull(context);
        Assert.False(context.IsActivated);
    }
}
