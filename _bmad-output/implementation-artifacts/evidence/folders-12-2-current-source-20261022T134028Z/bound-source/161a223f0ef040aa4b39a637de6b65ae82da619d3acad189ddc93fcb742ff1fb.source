using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>The exact installed tenant/domain feed. Installation changes require explicit new coverage.</summary>
public sealed record SourcePublicationScope
{
    /// <summary>Validates canonical identity components without admitting foreign tenant or key separators.</summary>
    public SourcePublicationScope(string tenant, string domain, string feedName, string installationId)
    {
        var feed = new AggregateIdentity(tenant, domain, feedName);
        _ = new AggregateIdentity(tenant, domain, installationId);
        Tenant = feed.TenantId; Domain = feed.Domain; FeedName = feed.AggregateId; InstallationId = installationId;
    }
    /// <summary>Gets the canonical tenant.</summary>
    public string Tenant { get; }
    /// <summary>Gets the canonical source domain.</summary>
    public string Domain { get; }
    /// <summary>Gets the closed projection/feed identity.</summary>
    public string FeedName { get; }
    /// <summary>Gets the independently installed coverage identity.</summary>
    public string InstallationId { get; }
    /// <summary>Gets the exact private technical index actor address.</summary>
    public string ActorId => new AggregateIdentity(Tenant, Domain, "publication-index-" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(FeedName + ":" + InstallationId)))).ActorId;
}
