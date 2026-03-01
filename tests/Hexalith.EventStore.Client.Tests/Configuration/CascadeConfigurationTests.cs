
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
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Client.Tests.Configuration;

public class CascadeConfigurationTests : IDisposable {
    public CascadeConfigurationTests() {
        AssemblyScanner.ClearCache();
        NamingConventionEngine.ClearCache();
    }

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
    public void AddEventStore_WithConfiguration_BindsEventStoreSectionToOptions() {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:DefaultStateStoreSuffix"] = "store",
                ["EventStore:DefaultTopicSuffix"] = "domain-events",
            })
            .Build();

        ServiceCollection services = new();
        _ = services.AddSingleton(configuration);
        _ = services.AddEventStore(typeof(Discovery.SmokeTestAggregate).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        EventStoreOptions options = provider.GetRequiredService<IOptions<EventStoreOptions>>().Value;

        Assert.Equal("store", options.DefaultStateStoreSuffix);
        Assert.Equal("domain-events", options.DefaultTopicSuffix);
    }

    [Fact]
    public void UseEventStore_WithAppSettingsGlobalSuffix_AppliesLayer2FromBoundOptions() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config => {
                _ = config.AddInMemoryCollection(new Dictionary<string, string?> {
                    ["EventStore:DefaultStateStoreSuffix"] = "store",
                });
            })
            .ConfigureServices(s => s.AddEventStore(typeof(Discovery.SmokeTestAggregate).Assembly))
            .Build();

        _ = host.UseEventStore();

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        EventStoreDomainActivation aggregate = Assert.Single(
            context.Activations,
            a => a.Kind == DomainKind.Aggregate && a.Type == typeof(Discovery.SmokeTestAggregate));

        Assert.Equal($"{aggregate.DomainName}-store", aggregate.StateStoreName);
    }

    [Fact]
    public void ResolveDomainOptions_ProjectionOnConfiguring_Works() {
        DiscoveredDomain domain = CreateDomain<ConfiguredInternalProjection>("configured-internal", DomainKind.Projection);
        EventStoreOptions globalOptions = new();

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, null, NullLogger.Instance);

        Assert.Equal("custom-projection-store", resolved.StateStoreName);
    }

    // --- Story 16-8: Layer 5 overrides Layer 4 (AC#3: 4.1) ---

    [Fact]
    public void ResolveDomainOptions_Layer5OverridesLayer4_OnSameProperty() {
        DiscoveredDomain domain = CreateDomain<CascadeInternalAggregate>("cascade-test");
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:Domains:cascade-test:StateStoreName"] = "appsettings-store",
                ["EventStore:Domains:cascade-test:TopicPattern"] = "appsettings-topic",
            })
            .Build();
        EventStoreOptions globalOptions = new();
        globalOptions.ConfigureDomain("cascade-test", d => d.StateStoreName = "explicit-store");

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, config, NullLogger.Instance);

        // Layer 5 overrides Layer 4 for StateStoreName
        Assert.Equal("explicit-store", resolved.StateStoreName);
        // Layer 4 still applies for TopicPattern (no Layer 5 override)
        Assert.Equal("appsettings-topic", resolved.TopicPattern);
    }

    // --- Story 16-8: Layer 4 overrides Layer 3 (AC#3: 4.2) ---

    [Fact]
    public void ResolveDomainOptions_Layer4OverridesLayer3_OnSameProperty() {
        DiscoveredDomain domain = CreateDomain<ConfiguredInternalAggregate>("configured-test");
        // ConfiguredInternalAggregate.OnConfiguring sets StateStoreName = "custom-configured-store" (Layer 3)
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:Domains:configured-test:StateStoreName"] = "appsettings-overrides-layer3",
            })
            .Build();
        EventStoreOptions globalOptions = new();

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, config, NullLogger.Instance);

        // Layer 4 (appsettings) overrides Layer 3 (OnConfiguring) for StateStoreName
        Assert.Equal("appsettings-overrides-layer3", resolved.StateStoreName);
    }

    // --- Story 16-8: Partial override - one property set, others fall through (AC#3: 4.3) ---

    [Fact]
    public void ResolveDomainOptions_PartialLayer3_OthersFallThroughFromConvention() {
        // ConfiguredInternalAggregate only sets StateStoreName in OnConfiguring
        DiscoveredDomain domain = CreateDomain<ConfiguredInternalAggregate>("configured-test");
        EventStoreOptions globalOptions = new();

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, null, NullLogger.Instance);

        // Layer 3 sets StateStoreName
        Assert.Equal("custom-configured-store", resolved.StateStoreName);
        // TopicPattern and DeadLetterTopicPattern fall through from Layer 1 (convention)
        Assert.Equal("configured-test.events", resolved.TopicPattern);
        Assert.Equal("deadletter.configured-test.events", resolved.DeadLetterTopicPattern);
    }

    // --- Story 16-8: Multi-domain independent resolution (AC#3: 4.4) ---

    [Fact]
    public void ResolveDomainOptions_TwoDomains_ResolvedIndependently() {
        DiscoveredDomain domainA = CreateDomain<CascadeInternalAggregate>("domain-a");
        DiscoveredDomain domainB = CreateDomain<ConfiguredInternalAggregate>("domain-b");
        EventStoreOptions globalOptions = new();
        globalOptions.ConfigureDomain("domain-a", d => d.StateStoreName = "a-explicit-store");
        // domain-b has NO Layer 5 override

        EventStoreDomainOptions resolvedA = EventStoreHostExtensions.ResolveDomainOptions(domainA, globalOptions, null, NullLogger.Instance);
        EventStoreDomainOptions resolvedB = EventStoreHostExtensions.ResolveDomainOptions(domainB, globalOptions, null, NullLogger.Instance);

        // domain-a gets Layer 5 override
        Assert.Equal("a-explicit-store", resolvedA.StateStoreName);
        // domain-b gets Layer 3 override (OnConfiguring sets "custom-configured-store")
        Assert.Equal("custom-configured-store", resolvedB.StateStoreName);
        // Both have independent topic patterns
        Assert.Equal("domain-a.events", resolvedA.TopicPattern);
        Assert.Equal("domain-b.events", resolvedB.TopicPattern);
    }

    // --- Story 16-8: ConfigureDomain with null/whitespace rejection (AC#3: 4.5) ---
    // (Already covered by existing tests, but verify case-insensitive matching)

    [Fact]
    public void ConfigureDomain_CaseInsensitive_MatchesDiscoveredDomainName() {
        DiscoveredDomain domain = CreateDomain<CascadeInternalAggregate>("cascade-test");
        EventStoreOptions globalOptions = new();
        // Register with different case — DomainConfigurations uses OrdinalIgnoreCase
        globalOptions.ConfigureDomain("Cascade-Test", d => d.StateStoreName = "case-insensitive-store");

        EventStoreDomainOptions resolved = EventStoreHostExtensions.ResolveDomainOptions(domain, globalOptions, null, NullLogger.Instance);

        Assert.Equal("case-insensitive-store", resolved.StateStoreName);
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
