
using System.Reflection;
using System.Runtime.CompilerServices;

using Hexalith.EventStore.Client.Configuration;
using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Tests.Discovery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Client.Tests.Registration;

public class AddEventStoreTests : IDisposable {
    public void Dispose() {
        AssemblyScanner.ClearCache();
        NamingConventionEngine.ClearCache();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AddEventStore_ZeroConfigOverload_DiscoversAndRegistersAggregatesFromCallingAssembly() {
        var services = new ServiceCollection();

        _ = AddEventStoreViaNoInlineHelper(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        IDomainProcessor processor = provider.GetRequiredService<IDomainProcessor>();

        Assert.NotNull(processor);
        Assert.IsType<SmokeTestAggregate>(processor);
    }

    [Fact]
    public void AddEventStore_ExplicitAssembly_RegistersAggregateAsIDomainProcessor() {
        var services = new ServiceCollection();

        _ = services.AddEventStore(typeof(SmokeTestAggregate).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        IDomainProcessor processor = provider.GetRequiredService<IDomainProcessor>();

        Assert.NotNull(processor);
        Assert.IsType<SmokeTestAggregate>(processor);
    }

    [Fact]
    public void AddEventStore_WithOptions_RegistersOptionsInDI() {
        var services = new ServiceCollection();

        _ = services.AddEventStore(
            options => options.EnableRegistrationDiagnostics = true,
            typeof(SmokeTestAggregate).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        IOptions<EventStoreOptions> options = provider.GetRequiredService<IOptions<EventStoreOptions>>();

        Assert.NotNull(options);
        Assert.NotNull(options.Value);
        Assert.True(options.Value.EnableRegistrationDiagnostics);
    }

    [Fact]
    public void AddEventStore_ExplicitAssembly_DiscoversTypesFromSpecifiedAssembly() {
        var services = new ServiceCollection();

        _ = services.AddEventStore(typeof(SmokeTestAggregate).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        DiscoveryResult result = provider.GetRequiredService<DiscoveryResult>();

        Assert.NotNull(result);
        Assert.Contains(result.Aggregates, a => a.Type == typeof(SmokeTestAggregate));
    }

    [Fact]
    public void AddEventStore_KeyedServiceRegistration_ResolvesCorrectAggregateByDomainName() {
        var services = new ServiceCollection();

        _ = services.AddEventStore(typeof(SmokeTestAggregate).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        DiscoveryResult result = provider.GetRequiredService<DiscoveryResult>();
        string domainName = result.Aggregates.Single(a => a.Type == typeof(SmokeTestAggregate)).DomainName;

        using IServiceScope scope = provider.CreateScope();
        IDomainProcessor keyed = scope.ServiceProvider.GetRequiredKeyedService<IDomainProcessor>(domainName);

        Assert.NotNull(keyed);
        Assert.IsType<SmokeTestAggregate>(keyed);
    }

    [Fact]
    public void AddEventStore_RegistersDiscoveryResultAsSingleton() {
        var services = new ServiceCollection();

        _ = services.AddEventStore(typeof(SmokeTestAggregate).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        DiscoveryResult result1 = provider.GetRequiredService<DiscoveryResult>();
        DiscoveryResult result2 = provider.GetRequiredService<DiscoveryResult>();

        Assert.Same(result1, result2);
    }

    [Fact]
    public void AddEventStore_CalledTwice_DoesNotDuplicateRegistrations() {
        var services = new ServiceCollection();

        _ = services.AddEventStore(typeof(SmokeTestAggregate).Assembly);
        _ = services.AddEventStore(typeof(SmokeTestAggregate).Assembly);

        int discoveryResultCount = services.Count(s => s.ServiceType == typeof(DiscoveryResult));
        Assert.Equal(1, discoveryResultCount);
    }

    [Fact]
    public void AddEventStore_NullServices_ThrowsArgumentNullException() {
        IServiceCollection services = null!;

        _ = Assert.Throws<ArgumentNullException>(() => services.AddEventStore(typeof(SmokeTestAggregate).Assembly));
    }

    [Fact]
    public void AddEventStore_NullConfigureOptions_ThrowsArgumentNullException() {
        var services = new ServiceCollection();

        _ = Assert.Throws<ArgumentNullException>(() => services.AddEventStore((Action<EventStoreOptions>)null!, typeof(SmokeTestAggregate).Assembly));
    }

    [Fact]
    public void AddEventStore_NullAssemblies_ThrowsArgumentNullException() {
        var services = new ServiceCollection();

        _ = Assert.Throws<ArgumentNullException>(() => services.AddEventStore(assemblies: null!));
    }

    [Fact]
    public void AddEventStore_EmptyAssemblies_ThrowsArgumentException() {
        var services = new ServiceCollection();

        _ = Assert.Throws<ArgumentException>(() => services.AddEventStore(assemblies: Array.Empty<Assembly>()));
    }

    [Fact]
    public void AddEventStoreClient_StillWorksAlongsideAddEventStore() {
        var services = new ServiceCollection();

        _ = services.AddEventStore(typeof(SmokeTestAggregate).Assembly);
        _ = services.AddEventStoreClient<SmokeTestAggregate>();

        using ServiceProvider provider = services.BuildServiceProvider();
        IEnumerable<IDomainProcessor> processors = provider.GetServices<IDomainProcessor>();

        Assert.NotEmpty(processors);
    }

    [Fact]
    public void AddEventStore_AggregateResolvesAsIDomainProcessor() {
        var services = new ServiceCollection();

        _ = services.AddEventStore(typeof(SmokeTestAggregate).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IEnumerable<IDomainProcessor> processors = scope.ServiceProvider.GetServices<IDomainProcessor>();

        Assert.Contains(processors, p => p is SmokeTestAggregate);
    }

    [Fact]
    public void AddEventStore_OptionsAndAssemblies_BothWork() {
        var services = new ServiceCollection();

        _ = services.AddEventStore(
            options => { },
            typeof(SmokeTestAggregate).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        IOptions<EventStoreOptions> options = provider.GetRequiredService<IOptions<EventStoreOptions>>();
        Assert.NotNull(options.Value);

        DiscoveryResult result = provider.GetRequiredService<DiscoveryResult>();
        Assert.Contains(result.Aggregates, a => a.Type == typeof(SmokeTestAggregate));
    }

    [Fact]
    public void AddEventStore_ProjectionsDiscoveredButNotRegistered() {
        var services = new ServiceCollection();

        _ = services.AddEventStore(typeof(SmokeTestProjection).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        DiscoveryResult result = provider.GetRequiredService<DiscoveryResult>();

        // Projections should be in DiscoveryResult
        Assert.Contains(result.Projections, p => p.Type == typeof(SmokeTestProjection));

        // Projections should NOT be registered as services — only aggregates are registered
        int projectionRegistrations = services.Count(s =>
            s.ServiceType == typeof(IDomainProcessor) &&
            s.ImplementationType == typeof(SmokeTestProjection));
        Assert.Equal(0, projectionRegistrations);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IServiceCollection AddEventStoreViaNoInlineHelper(IServiceCollection services) => services.AddEventStore();
}
