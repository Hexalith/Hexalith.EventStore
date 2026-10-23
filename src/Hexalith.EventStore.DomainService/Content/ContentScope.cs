using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.DomainService.Content;

/// <summary>The canonical tenant, aggregate and workspace owning authoritative bodies.</summary>
/// <param name="TenantId">Canonical managed tenant.</param>
/// <param name="Domain">Canonical domain.</param>
/// <param name="AggregateId">Owning aggregate identity.</param>
/// <param name="WorkspaceId">Opaque workspace identity.</param>
public sealed record ContentScope(string TenantId, string Domain, string AggregateId, string WorkspaceId)
{
    /// <summary>Rejects aliases, invalid identities and platform scope.</summary>
    public void Validate()
    {
        var identity = new AggregateIdentity(TenantId, Domain, AggregateId);
        if (TenantId == "system" || identity.TenantId != TenantId || identity.Domain != Domain)
            throw new ArgumentException("Content requires a canonical managed tenant and domain.");
        ArgumentException.ThrowIfNullOrWhiteSpace(WorkspaceId);
        if (WorkspaceId.Length > 128) throw new ArgumentException("Content workspace identity is too long.");
    }
}
