
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Configuration;
using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Registration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.EventStore.Client.Tests.Configuration;

public class CascadeConfigurationTests : IDisposable {
    public void Dispose() {
        AssemblyScanner.ClearCache();
        NamingConventionEngine.ClearCache();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void EventStoreDomainOptions_DefaultProperties_AreAllNull() {
        EventStoreDomainOptions options = new();

        Assert.Null(options.StateStoreName);
        Assert.Null(options.TopicPattern);
        Assert.Null(options.DeadLetterTopicPattern);
    }

    [Fact]
    public void ConfigureDomain_NullDomainName_ThrowsArgumentException() {
        EventStoreOptions options = new();

        _ = Assert.Throws<ArgumentNullException>(() => options.ConfigureDomain(null!, _ => { }));
    }

    [Fact]
    public void ConfigureDomain_EmptyDomainName_ThrowsArgumentException() {
        EventStoreOptions options = new();

        _ = Assert.Throws<ArgumentException>(() => options.ConfigureDomain("", _ => { }));
    }

    [Fact]
    public void ConfigureDomain_WhitespaceDomainName_ThrowsArgumentException() {
        EventStoreOptions options = new();

        _ = Assert.Throws<ArgumentException>(() => options.ConfigureDomain("  ", _ => { }));
    }

    [Fact]
    public void ConfigureDomain_NullConfigure_ThrowsArgumentNullException() {
        EventStoreOptions options = new();

        _ = Assert.Throws<ArgumentNullException>(() => options.ConfigureDomain("counter", null!));
    }

    [Fact]
    public void ResolveDomainOptions_ConventionOnly_ProducesConventionDefaults() {
        DiscoveredDomain domain = CreateDomain<CascadeInternalAggregate>("cascade-test");
        EventStoreOptions globalOptions = new();

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, null, NullLogger.Instance);

        Assert.Equal("cascade-test-eventstore", resolved.StateStoreName);
        Assert.Equal("cascade-test.events", resolved.TopicPattern);
        Assert.Equal("deadletter.cascade-test.events", resolved.DeadLetterTopicPattern);
    }

    [Fact]
    public void ResolveDomainOptions_GlobalSuffix_OverridesConventionSuffix() {
        DiscoveredDomain domain = CreateDomain<CascadeInternalAggregate>("cascade-test");
        EventStoreOptions globalOptions = new() { DefaultStateStoreSuffix = "store" };

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, null, NullLogger.Instance);

        Assert.Equal("cascade-test-store", resolved.StateStoreName);
        Assert.Equal("cascade-test.events", resolved.TopicPattern);
    }

    [Fact]
    public void ResolveDomainOptions_GlobalTopicSuffix_OverridesConventionTopicSuffix() {
        DiscoveredDomain domain = CreateDomain<CascadeInternalAggregate>("cascade-test");
        EventStoreOptions globalOptions = new() { DefaultTopicSuffix = "domain-events" };

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, null, NullLogger.Instance);

        Assert.Equal("cascade-test.domain-events", resolved.TopicPattern);
        Assert.Equal("deadletter.cascade-test.domain-events", resolved.DeadLetterTopicPattern);
    }

    [Fact]
    public void ResolveDomainOptions_OnConfiguring_OverridesPerDomainValues() {
        DiscoveredDomain domain = CreateDomain<ConfiguredInternalAggregate>("configured-test");
        EventStoreOptions globalOptions = new();

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, null, NullLogger.Instance);

        Assert.Equal("custom-configured-store", resolved.StateStoreName);
        Assert.Equal("configured-test.events", resolved.TopicPattern);
    }

    [Fact]
    public void ResolveDomainOptions_AppSettings_OverridesConvention() {
        DiscoveredDomain domain = CreateDomain<CascadeInternalAggregate>("cascade-test");
        EventStoreOptions globalOptions = new();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:Domains:cascade-test:StateStoreName"] = "appsettings-store",
            })
            .Build();

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, config, NullLogger.Instance);

        Assert.Equal("appsettings-store", resolved.StateStoreName);
        Assert.Equal("cascade-test.events", resolved.TopicPattern);
    }

    [Fact]
    public void ResolveDomainOptions_ConfigureDomain_TakesHighestPriority() {
        DiscoveredDomain domain = CreateDomain<CascadeInternalAggregate>("cascade-test");
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:Domains:cascade-test:StateStoreName"] = "appsettings-store",
            })
            .Build();
        EventStoreOptions globalOptions = new();
        globalOptions.ConfigureDomain("cascade-test", d => d.StateStoreName = "explicit-override-store");

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, config, NullLogger.Instance);

        Assert.Equal("explicit-override-store", resolved.StateStoreName);
    }

    [Fact]
    public void ResolveDomainOptions_PartialOverride_NonOverriddenPropertiesKeepConvention() {
        DiscoveredDomain domain = CreateDomain<CascadeInternalAggregate>("cascade-test");
        EventStoreOptions globalOptions = new();
        globalOptions.ConfigureDomain("cascade-test", d => d.StateStoreName = "custom-store");

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, null, NullLogger.Instance);

        Assert.Equal("custom-store", resolved.StateStoreName);
        Assert.Equal("cascade-test.events", resolved.TopicPattern);
        Assert.Equal("deadletter.cascade-test.events", resolved.DeadLetterTopicPattern);
    }

    [Fact]
    public void ResolveDomainOptions_AllFiveLayersActive_CorrectPriorityPerProperty() {
        DiscoveredDomain domain = CreateDomain<ConfiguredInternalAggregate>("configured-test");
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:Domains:configured-test:TopicPattern"] = "config-topic",
            })
            .Build();
        EventStoreOptions globalOptions = new() { DefaultStateStoreSuffix = "global-store" };
        globalOptions.ConfigureDomain("configured-test", d => d.DeadLetterTopicPattern = "explicit-deadletter");

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, config, NullLogger.Instance);

        // Layer 3 (OnConfiguring) overrides Layer 2 for StateStoreName
        Assert.Equal("custom-configured-store", resolved.StateStoreName);
        // Layer 4 (appsettings) overrides convention for TopicPattern
        Assert.Equal("config-topic", resolved.TopicPattern);
        // Layer 5 (explicit) overrides all for DeadLetterTopicPattern
        Assert.Equal("explicit-deadletter", resolved.DeadLetterTopicPattern);
    }

    [Fact]
    public void ResolveDomainOptions_NoParameterlessConstructor_SkipsLayer3Gracefully() {
        DiscoveredDomain domain = CreateDomain<NoCtorInternalAggregate>("no-ctor-test");
        EventStoreOptions globalOptions = new();
        globalOptions.ConfigureDomain("no-ctor-test", d => d.StateStoreName = "layer5-store");

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, null, NullLogger.Instance);

        Assert.Equal("layer5-store", resolved.StateStoreName);
        Assert.Equal("no-ctor-test.events", resolved.TopicPattern);
    }

    [Fact]
    public void ResolveDomainOptions_MissingConfigSection_SkipsLayer4() {
        DiscoveredDomain domain = CreateDomain<CascadeInternalAggregate>("cascade-test");
        EventStoreOptions globalOptions = new();
        IConfiguration config = new ConfigurationBuilder().Build();

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, config, NullLogger.Instance);

        Assert.Equal("cascade-test-eventstore", resolved.StateStoreName);
        Assert.Equal("cascade-test.events", resolved.TopicPattern);
        Assert.Equal("deadletter.cascade-test.events", resolved.DeadLetterTopicPattern);
    }

    [Fact]
    public void UseEventStore_ConventionOnly_BackwardCompatibility() {
        // Uses SmokeTestAggregate from Discovery namespace — validates no behavior change
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => s.AddEventStore(typeof(Discovery.SmokeTestAggregate).Assembly))
            .Build();

        _ = host.UseEventStore();

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        Assert.True(context.IsActivated);
        Assert.NotEmpty(context.Activations);

        // Verify convention defaults for SmokeTestAggregate specifically
        EventStoreDomainActivation aggregate = Assert.Single(
            context.Activations,
            a => a.Kind == DomainKind.Aggregate && a.Type == typeof(Discovery.SmokeTestAggregate));
        Assert.Equal($"{aggregate.DomainName}-eventstore", aggregate.StateStoreName);
        Assert.Equal($"{aggregate.DomainName}.events", aggregate.TopicPattern);
        Assert.Equal($"deadletter.{aggregate.DomainName}.events", aggregate.DeadLetterTopicPattern);
    }

    [Fact]
    public void ResolveDomainOptions_ProjectionOnConfiguring_Works() {
        DiscoveredDomain domain = CreateDomain<ConfiguredInternalProjection>("configured-internal", DomainKind.Projection);
        EventStoreOptions globalOptions = new();

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, null, NullLogger.Instance);

        Assert.Equal("custom-projection-store", resolved.StateStoreName);
    }

    private static DiscoveredDomain CreateDomain<T>(string domainName, DomainKind kind = DomainKind.Aggregate) =>
        new(typeof(T), domainName, typeof(object), kind);
}

// === Internal Test Stubs (not discoverable by AssemblyScanner.GetExportedTypes()) ===

/// <summary>Simple internal aggregate for cascade testing.</summary>
internal sealed class CascadeInternalAggregate : EventStoreAggregate<CascadeInternalState> { }

/// <summary>State for cascade internal aggregate.</summary>
internal sealed class CascadeInternalState { }

/// <summary>Internal aggregate with OnConfiguring override (Layer 3).</summary>
internal sealed class ConfiguredInternalAggregate : EventStoreAggregate<ConfiguredInternalState> {
    /// <inheritdoc/>
    protected override void OnConfiguring(EventStoreDomainOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        options.StateStoreName = "custom-configured-store";
    }
}

/// <summary>State for configured internal aggregate.</summary>
internal sealed class ConfiguredInternalState { }

/// <summary>Internal aggregate without parameterless constructor to test Layer 3 skip.</summary>
internal sealed class NoCtorInternalAggregate : EventStoreAggregate<NoCtorInternalState> {
#pragma warning disable IDE0052 // Remove unread private member
    private readonly string _required;
#pragma warning restore IDE0052

    /// <summary>Initializes a new instance with a required parameter.</summary>
    public NoCtorInternalAggregate(string required) {
        _required = required;
    }
}

/// <summary>State for no-ctor internal aggregate.</summary>
internal sealed class NoCtorInternalState { }

/// <summary>Internal projection with OnConfiguring override.</summary>
internal sealed class ConfiguredInternalProjection : EventStoreProjection<ConfiguredInternalReadModel> {
    /// <inheritdoc/>
    protected override void OnConfiguring(EventStoreDomainOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        options.StateStoreName = "custom-projection-store";
    }
}

/// <summary>Read model for configured internal projection.</summary>
internal sealed class ConfiguredInternalReadModel { }
