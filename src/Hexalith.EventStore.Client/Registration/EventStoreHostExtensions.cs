
using System.Text;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Configuration;
using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Discovery;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Client.Registration;

/// <summary>
/// Extension methods on <see cref="IHost"/> for runtime activation of EventStore domain types.
/// This prepares the activation manifest (cascade-resolved DAPR resource names) — it does NOT
/// directly wire DAPR subscriptions or middleware. The Client SDK provides the "what" (which domains
/// exist, their resource names); the server provides the "how" (actual DAPR wiring).
/// </summary>
public static class EventStoreHostExtensions {
    /// <summary>
    /// Prepares the runtime activation manifest from auto-discovered domain types registered by <c>AddEventStore()</c>.
    /// Resolves per-domain configuration through the five-layer cascade and populates the
    /// <see cref="EventStoreActivationContext"/> singleton.
    /// </summary>
    /// <param name="host">The host instance. <c>WebApplication</c> implements <c>IHost</c>, so this works for web apps too.</param>
    /// <returns>The host for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="host"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>AddEventStore()</c> was not called during service registration.
    /// </exception>
    public static IHost UseEventStore(this IHost host) {
        ArgumentNullException.ThrowIfNull(host);

        // Resolve DiscoveryResult — fail fast if AddEventStore() was never called
        DiscoveryResult? discoveryResult = host.Services.GetService<DiscoveryResult>();
        if (discoveryResult is null) {
            throw new InvalidOperationException(
                "UseEventStore() requires AddEventStore() to be called first during service registration. " +
                "Ensure builder.Services.AddEventStore() is called before building the host.");
        }

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        EventStoreOptions options = host.Services.GetRequiredService<IOptions<EventStoreOptions>>().Value;
        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(EventStoreHostExtensions));
        IConfiguration? configuration = host.Services.GetService<IConfiguration>();

        // Build activation list from discovered domains using cascade resolution
        var activations = new List<EventStoreDomainActivation>();
        foreach (DiscoveredDomain domain in discoveryResult.Aggregates.Concat(discoveryResult.Projections)) {
            EventStoreDomainOptions resolved = ResolveDomainOptions(domain, options, configuration, logger);
            activations.Add(new EventStoreDomainActivation(
                DomainName: domain.DomainName,
                Kind: domain.Kind,
                Type: domain.Type,
                StateType: domain.StateType,
                StateStoreName: resolved.StateStoreName ?? throw new InvalidOperationException($"StateStoreName resolved to null for domain '{domain.DomainName}'."),
                TopicPattern: resolved.TopicPattern ?? throw new InvalidOperationException($"TopicPattern resolved to null for domain '{domain.DomainName}'."),
                DeadLetterTopicPattern: resolved.DeadLetterTopicPattern ?? throw new InvalidOperationException($"DeadLetterTopicPattern resolved to null for domain '{domain.DomainName}'.")));
        }

        // Idempotency check
        if (!context.TryActivate(activations.AsReadOnly())) {
            logger.LogWarning("UseEventStore() has already been called. Skipping duplicate activation.");
            return host;
        }

        // Summary logging
        if (activations.Count == 0) {
            logger.LogInformation("EventStore activated: 0 domains");
        } else {
            StringBuilder sb = new();
            for (int i = 0; i < activations.Count; i++) {
                if (i > 0) {
                    _ = sb.Append(", ");
                }

                EventStoreDomainActivation a = activations[i];
                _ = sb.Append($"{a.DomainName} [{a.Kind}: {a.Type.Name}]");
            }

            logger.LogInformation("EventStore activated: {DomainCount} domains ({DomainDetails})", activations.Count, sb.ToString());
        }

        // Diagnostics logging
        if (options.EnableRegistrationDiagnostics) {
            foreach (EventStoreDomainActivation a in activations) {
                logger.LogDebug(
                    "EventStore domain: {DomainName}, StateStore={StateStoreName}, Topic={TopicPattern}, DeadLetter={DeadLetterTopicPattern}, Type={TypeFullName}, StateType={StateTypeFullName}",
                    a.DomainName,
                    a.StateStoreName,
                    a.TopicPattern,
                    a.DeadLetterTopicPattern,
                    a.Type.FullName,
                    a.StateType.FullName);
            }
        }

        return host;
    }

    /// <summary>
    /// Resolves per-domain options through the five-layer cascade.
    /// </summary>
    internal static EventStoreDomainOptions ResolveDomainOptions(
        DiscoveredDomain domain,
        EventStoreOptions globalOptions,
        IConfiguration? configuration,
        ILogger? logger) {
        bool diagnostics = globalOptions.EnableRegistrationDiagnostics;
        var layers = new List<string>();

        // Layer 1: Convention defaults (always applied)
        var resolved = new EventStoreDomainOptions {
            StateStoreName = NamingConventionEngine.GetStateStoreName(domain.DomainName),
            TopicPattern = $"{domain.DomainName}.events",
            DeadLetterTopicPattern = $"deadletter.{domain.DomainName}.events",
        };
        if (diagnostics) {
            layers.Add("Layer 1 (convention)");
        }

        // Layer 2: Global overrides from EventStoreOptions
        bool layer2Applied = false;
        if (globalOptions.DefaultStateStoreSuffix is not null) {
            resolved.StateStoreName = $"{domain.DomainName}-{globalOptions.DefaultStateStoreSuffix}";
            layer2Applied = true;
        }

        if (globalOptions.DefaultTopicSuffix is not null) {
            resolved.TopicPattern = $"{domain.DomainName}.{globalOptions.DefaultTopicSuffix}";
            resolved.DeadLetterTopicPattern = $"deadletter.{domain.DomainName}.{globalOptions.DefaultTopicSuffix}";
            layer2Applied = true;
        }

        if (diagnostics && layer2Applied) {
            layers.Add("Layer 2 (global options)");
        }

        // Layer 3: Domain self-config (OnConfiguring)
        try {
            object? instance = Activator.CreateInstance(domain.Type);
            EventStoreDomainOptions domainOpts = new();
            bool invoked = false;

            // Use reflection to call InvokeOnConfiguring since we don't know TState at compile time
            System.Reflection.MethodInfo? invokeMethod = domain.Type.GetMethod(
                "InvokeOnConfiguring",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (invokeMethod is not null) {
                invokeMethod.Invoke(instance, [domainOpts]);
                invoked = true;
            }

            if (invoked) {
                bool merged = MergeNonNull(resolved, domainOpts);
                if (diagnostics && merged) {
                    layers.Add("Layer 3 (OnConfiguring)");
                }
            }
        }
        catch (MissingMethodException ex) {
            logger?.LogDebug(
                "Domain '{DomainName}': skipping OnConfiguring (no parameterless constructor: {Error})",
                domain.DomainName,
                ex.Message);
        }
        catch (System.Reflection.TargetInvocationException ex) {
            logger?.LogDebug(
                "Domain '{DomainName}': skipping OnConfiguring (invocation error: {Error})",
                domain.DomainName,
                ex.InnerException?.Message ?? ex.Message);
        }
        catch (Exception ex) {
            logger?.LogDebug(
                "Domain '{DomainName}': skipping OnConfiguring (activation error: {Error})",
                domain.DomainName,
                ex.Message);
        }

        // Layer 4: appsettings.json
        if (configuration is not null) {
            IConfigurationSection section = configuration.GetSection($"EventStore:Domains:{domain.DomainName}");
            if (section.Exists()) {
                EventStoreDomainOptions configOpts = new();
                section.Bind(configOpts);
                bool merged = MergeNonNull(resolved, configOpts);
                if (diagnostics && merged) {
                    layers.Add("Layer 4 (appsettings.json)");
                }
            }
        }

        // Layer 5: Explicit overrides from ConfigureDomain()
        if (globalOptions.DomainConfigurations.TryGetValue(domain.DomainName, out Action<EventStoreDomainOptions>? configure)) {
            EventStoreDomainOptions explicitOpts = new();
            configure(explicitOpts);
            bool merged = MergeNonNull(resolved, explicitOpts);
            if (diagnostics && merged) {
                layers.Add("Layer 5 (ConfigureDomain)");
            }
        }

        // Diagnostic log for cascade resolution
        if (diagnostics && logger is not null) {
            logger.LogDebug(
                "Domain '{DomainName}' configuration resolved: {Layers}",
                domain.DomainName,
                string.Join(", ", layers));
        }

        return resolved;
    }

    /// <summary>
    /// Merges non-null values from source into target.
    /// </summary>
    /// <returns><c>true</c> if any value was merged.</returns>
    private static bool MergeNonNull(EventStoreDomainOptions target, EventStoreDomainOptions source) {
        bool merged = false;
        if (source.StateStoreName is not null) {
            target.StateStoreName = source.StateStoreName;
            merged = true;
        }

        if (source.TopicPattern is not null) {
            target.TopicPattern = source.TopicPattern;
            merged = true;
        }

        if (source.DeadLetterTopicPattern is not null) {
            target.DeadLetterTopicPattern = source.DeadLetterTopicPattern;
            merged = true;
        }

        return merged;
    }
}
