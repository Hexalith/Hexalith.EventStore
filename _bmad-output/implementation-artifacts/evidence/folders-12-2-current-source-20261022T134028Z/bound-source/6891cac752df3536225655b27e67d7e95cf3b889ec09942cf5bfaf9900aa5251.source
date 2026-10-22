using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Client.Processes;

/// <summary>One canonical, tenant-scoped source stream.</summary>
/// <param name="Tenant">Canonical tenant.</param>
/// <param name="Domain">Canonical domain.</param>
/// <param name="Aggregate">Aggregate identifier.</param>
public sealed record ProcessSource(string Tenant, string Domain, string Aggregate)
{
    /// <summary>Stable ordinal key used for mutation-safe catalog continuation.</summary>
    [JsonIgnore]
    public string CatalogKey => Tenant + "\0" + Domain + "\0" + Aggregate;

    /// <summary>Validates this source's canonical identity.</summary>
    public void Validate()
    {
        var identity = new AggregateIdentity(Tenant, Domain, Aggregate);
        if (!string.Equals(Tenant, identity.TenantId, StringComparison.Ordinal)
            || !string.Equals(Domain, identity.Domain, StringComparison.Ordinal))
        {
            throw new ArgumentException("Process source identity must be canonical.");
        }
    }
}
