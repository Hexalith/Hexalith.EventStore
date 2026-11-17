using System.Globalization;

using Hexalith.EventStore.Contracts.Snapshots;
using Hexalith.EventStore.DomainService;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Server.Configuration;

/// <summary>Enrolls explicit deployment-owned actor profiles without loading domain assemblies.</summary>
public static class FoldedSnapshotActorConfigurationExtensions
{
    /// <summary>The optional deployment configuration array of reviewed actor declarations.</summary>
    public const string SectionName = "EventStore:Snapshots:ActorQualifications";

    private static readonly HashSet<string> s_entryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Domain", "AggregateType", "ProfileId", "ParityEvidenceId", "Limits",
    };

    private static readonly HashSet<string> s_limitKeys = typeof(FoldedSnapshotCaptureLimits)
        .GetConstructors().Single().GetParameters().Select(parameter => parameter.Name!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Validates the complete optional configuration before adding any actor qualification.</summary>
    /// <param name="services">The actor host service collection.</param>
    /// <param name="configuration">Trusted serving-deployment configuration, never command input.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>Absent configuration preserves unenrolled behavior. Present invalid configuration fails closed.</remarks>
    public static IServiceCollection AddEventStoreConfiguredFoldedSnapshotActorQualifications(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        FoldedSnapshotQualification[] declarations;
        try
        {
            IConfigurationSection? section = configuration.GetSection("EventStore:Snapshots").GetChildren()
                .SingleOrDefault(child => string.Equals(child.Key, "ActorQualifications", StringComparison.OrdinalIgnoreCase));
            if (section is null)
            {
                return services;
            }
            IConfigurationSection[] entries = section.GetChildren().ToArray();
            if (section.Value is not null || entries.Length == 0)
            {
                throw InvalidConfiguration();
            }
            var parsed = new List<FoldedSnapshotQualification>();
            for (int index = 0; index < entries.Length; index++)
            {
                IConfigurationSection entry = entries[index];
                if (entry.Key != index.ToString(CultureInfo.InvariantCulture) || entry.Value is not null
                    || entry.GetChildren().Count() != s_entryKeys.Count
                    || entry.GetChildren().Any(child => !s_entryKeys.Contains(child.Key)))
                {
                    throw InvalidConfiguration();
                }
                IConfigurationSection limitsSection = entry.GetSection("Limits");
                IConfigurationSection[] limitEntries = limitsSection.GetChildren().ToArray();
                if (limitsSection.Value is not null || limitEntries.Length != s_limitKeys.Count
                    || limitEntries.Any(child => !s_limitKeys.Contains(child.Key) || child.Value is null || child.GetChildren().Any()))
                {
                    throw InvalidConfiguration();
                }
                FoldedSnapshotCaptureLimits limits = limitsSection.Get<FoldedSnapshotCaptureLimits>(
                    options => options.ErrorOnUnknownConfiguration = true) ?? throw InvalidConfiguration();
                parsed.Add(new FoldedSnapshotQualification(
                    Identity(entry, "Domain"), Identity(entry, "AggregateType"), typeof(object),
                    Identity(entry, "ProfileId"), Identity(entry, "ParityEvidenceId"), limits));
            }

            // Registry validation covers code/config collisions as well as duplicate config entries.
            var existing = new List<FoldedSnapshotQualification>();
            foreach (ServiceDescriptor descriptor in services.Where(item => item.ServiceType == typeof(FoldedSnapshotQualification)))
            {
                if (descriptor.IsKeyedService || descriptor.ImplementationInstance is not FoldedSnapshotQualification declaration)
                {
                    throw InvalidConfiguration();
                }
                existing.Add(declaration);
            }
            _ = new FoldedSnapshotQualificationRegistry(existing.Concat(parsed));
            declarations = parsed.ToArray();
        }
        catch
        {
            // Configuration can contain private deployment values. Never retain binder/constructor exceptions.
            throw InvalidConfiguration();
        }

        foreach (FoldedSnapshotQualification declaration in declarations)
        {
            services.AddEventStoreFoldedSnapshotActorQualification(declaration.Domain, declaration.AggregateType,
                declaration.ProfileId, declaration.ParityEvidenceId, declaration.Limits);
        }
        return services;
    }

    private static string Identity(IConfigurationSection entry, string key)
    {
        IConfigurationSection value = entry.GetSection(key);
        if (string.IsNullOrWhiteSpace(value.Value) || value.Value != value.Value.Trim() || value.GetChildren().Any())
        {
            throw InvalidConfiguration();
        }
        return value.Value;
    }

    private static InvalidOperationException InvalidConfiguration()
        => new("SnapshotActorQualificationConfigurationInvalid");
}
