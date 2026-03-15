using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Tests.Discovery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Client.Tests.Registration;

public class UseEventStoreTests : IDisposable {
    public UseEventStoreTests() {
        AssemblyScanner.ClearCache();
        NamingConventionEngine.ClearCache();
    }

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

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(host.UseEventStore);
        Assert.Equal(
            "UseEventStore() requires AddEventStore() to be called first during service registration. Ensure builder.Services.AddEventStore() is called before building the host.",
            ex.Message);
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

        _ = Assert.Throws<ArgumentNullException>(host.UseEventStore);
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

    // --- Story 16-8: Diagnostic logging verification (AC#4: 5.3) ---

    [Fact]
    public void UseEventStore_DiagnosticsEnabled_ProducesDebugLogOutput() {
        var logMessages = new List<string>();

        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureLogging(lb => {
                _ = lb.ClearProviders();
                _ = lb.AddProvider(new CapturingLoggerProvider(logMessages));
                _ = lb.SetMinimumLevel(LogLevel.Debug);
            })
            .ConfigureServices(s => s.AddEventStore(
                o => o.EnableRegistrationDiagnostics = true,
                typeof(SmokeTestAggregate).Assembly))
            .Build();

        _ = host.UseEventStore();

        // Diagnostics enabled should produce Debug-level messages about domain configuration
        Assert.Contains(logMessages, m => m.Contains("EventStore domain:"));
    }

    // --- Story 16-8: Activation context lifecycle in single test (AC#4: 5.4) ---

    [Fact]
    public void UseEventStore_ActivationContextLifecycle_FullSequence() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => s.AddEventStore(typeof(SmokeTestAggregate).Assembly))
            .Build();

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();

        // Before activation: IsActivated is false and Activations throws
        Assert.False(context.IsActivated);
        _ = Assert.Throws<InvalidOperationException>(() => context.Activations);

        // Activate
        _ = host.UseEventStore();

        // After activation: IsActivated is true and Activations returns data
        Assert.True(context.IsActivated);
        Assert.NotEmpty(context.Activations);
    }

    // --- Story 16-8: Mixed aggregate+projection activation count (AC#4: 5.2) ---

    [Fact]
    public void UseEventStore_MixedAggregateAndProjection_ActivationCountMatchesDiscovery() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => s.AddEventStore(typeof(SmokeTestAggregate).Assembly))
            .Build();

        _ = host.UseEventStore();

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        DiscoveryResult discovery = host.Services.GetRequiredService<DiscoveryResult>();

        // Activation count should equal total discovered domains
        Assert.Equal(discovery.TotalCount, context.Activations.Count);

        // Aggregates come first in iteration order (Concat order)
        int aggregateCount = context.Activations.Count(a => a.Kind == DomainKind.Aggregate);
        int projectionCount = context.Activations.Count(a => a.Kind == DomainKind.Projection);
        Assert.Equal(discovery.Aggregates.Count, aggregateCount);
        Assert.Equal(discovery.Projections.Count, projectionCount);
    }

    // --- Story 16-8: Layer 5 override through full IHost path (AC#4: 5.1) ---

    [Fact]
    public void UseEventStore_WithConfigureDomain_ReflectsOverrideInActivation() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => s.AddEventStore(
                o => o.ConfigureDomain("smoke-test", d => d.StateStoreName = "custom-smoke-store"),
                typeof(SmokeTestAggregate).Assembly))
            .Build();

        _ = host.UseEventStore();

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        EventStoreDomainActivation aggregate = Assert.Single(
            context.Activations,
            a => a.Kind == DomainKind.Aggregate && a.Type == typeof(SmokeTestAggregate));

        Assert.Equal("custom-smoke-store", aggregate.StateStoreName);
    }

    /// <summary>Simple capturing logger for testing log output.</summary>
    private sealed class CapturingLoggerProvider(List<string> messages) : ILoggerProvider {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);
        public void Dispose() { }
    }

    private sealed class CapturingLogger(List<string> messages) : ILogger {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            messages.Add(formatter(state, exception));
    }
}
