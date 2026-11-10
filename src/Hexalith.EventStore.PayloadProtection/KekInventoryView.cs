// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10.2-10.4; owner decision D1 (2026-11-10).
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Head-confirmed KEK-inventory evidence for the pages one operation needs; a complete snapshot converts to a view of every page.</summary>
/// <param name="BackendDigest">The exact backend address digest.</param>
/// <param name="ProviderDigest">The exact provider-version address digest.</param>
/// <param name="Head">The confirmed head and ETag, or null for an absent initial inventory.</param>
/// <param name="Pages">The read pages keyed by page number; a page not read is absent, never empty.</param>
internal sealed record KekInventoryView(string BackendDigest, string ProviderDigest, PayloadProtectionStateValue<KekInventoryHeadV1>? Head,
    IReadOnlyDictionary<uint, PayloadProtectionStateValue<KekInventoryPageV1>> Pages)
{
    /// <summary>Converts complete inventory evidence into a view holding every declared page.</summary>
    public static implicit operator KekInventoryView(KekKeyIndexSnapshot snapshot) => From(snapshot);

    /// <summary>Creates a view holding every page of complete inventory evidence.</summary>
    internal static KekInventoryView From(KekKeyIndexSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(snapshot.BackendDigest, snapshot.ProviderDigest, snapshot.Head, new SortedDictionary<uint, PayloadProtectionStateValue<KekInventoryPageV1>>(
            snapshot.Pages.ToDictionary(page => page.Value.Page)).AsReadOnly());
    }

    /// <summary>Formats no backend, provider or key identities.</summary>
    public override string ToString() => nameof(KekInventoryView);
}
