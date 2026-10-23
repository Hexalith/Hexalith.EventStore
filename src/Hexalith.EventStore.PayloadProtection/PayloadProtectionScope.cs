// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; amendment section 1.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>An explicitly enrolled tenant/domain root.</summary>
/// <param name="TenantId">The canonical tenant identity.</param>
/// <param name="Domain">The canonical protected domain.</param>
internal sealed record PayloadProtectionScope(string TenantId, string Domain)
{
    /// <summary>Returns only the bounded type name.</summary>
    public override string ToString() => nameof(PayloadProtectionScope);
}
