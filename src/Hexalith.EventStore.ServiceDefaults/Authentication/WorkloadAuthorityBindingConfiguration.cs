namespace Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

/// <summary>Preserves the platform's exact colon-delimited binding names across configuration providers.</summary>
internal static class WorkloadAuthorityBindingConfiguration
{
    /// <summary>Rebinds server-configured protocol claims to an unambiguous grant; malformed paths fail configuration.</summary>
    internal static void Configure(WorkloadAuthorityOptions options, IConfiguration configuration)
    {
        IConfigurationSection[] configuredGrants = [.. configuration.GetSection(WorkloadAuthorityOptions.SectionName + ":Grants").GetChildren()];
        foreach (IConfigurationSection configured in configuredGrants)
        {
            IConfigurationSection section = configured.GetSection("Bindings");
            if (!section.Exists()) continue;
            string? grantId = configured["GrantId"];
            WorkloadAuthorityGrant[] matches = options.Grants?.Where(grant => grant is not null
                && string.Equals(grant.GrantId, grantId, StringComparison.Ordinal)).ToArray() ?? [];
            if (string.IsNullOrWhiteSpace(grantId) || matches.Length != 1
                || configuredGrants.Count(grant => string.Equals(grant["GrantId"], grantId, StringComparison.Ordinal)) != 1)
                throw new OptionsValidationException(Options.DefaultName, typeof(WorkloadAuthorityOptions),
                    ["Configured workload authority bindings must identify exactly one matching grant."]);
            Dictionary<string, string[]> bindings = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string?> item in section.AsEnumerable(makePathsRelative: true))
            {
                bool hasChildren = section.GetSection(item.Key).GetChildren().Any();
                if (item.Value is null && (item.Key.Length == 0 || hasChildren)) continue;
                int separator = item.Key.LastIndexOf(':');
                string claim = separator < 0 ? string.Empty : item.Key[..separator];
                // Match WorkloadAuthorizationDetails.Decode's name/value bounds. The existing
                // decoder and broker still enforce mandatory Tenant/domain and exact grant matching.
                if (item.Value is null || hasChildren
                    || !claim.StartsWith(EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix, StringComparison.Ordinal)
                    || claim.Length > 128 || string.IsNullOrWhiteSpace(item.Value) || item.Value.Length > 256
                    || !int.TryParse(item.Key.AsSpan(separator + 1), System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out int ordinal) || ordinal < 0)
                    throw InvalidBindings();
                bindings.TryAdd(claim, section.GetSection(claim).Get<string[]>() ?? []);
                if (bindings.Count > 16) throw InvalidBindings();
            }
            matches[0].Bindings = bindings;
        }
    }

    private static OptionsValidationException InvalidBindings() => new(Options.DefaultName, typeof(WorkloadAuthorityOptions),
        ["Workload authority bindings must be bounded indexed lists of protocol-permitted platform claim names."]);
}
