// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; amendment section 1.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>An explicitly enrolled tenant/domain root.</summary>
/// <param name="TenantId">The canonical tenant identity.</param>
/// <param name="Domain">The canonical protected domain.</param>
internal sealed record PayloadProtectionScope(string TenantId, string Domain)
{
    /// <summary>Returns only the bounded type name.</summary>
    public override string ToString() => nameof(PayloadProtectionScope);
}
