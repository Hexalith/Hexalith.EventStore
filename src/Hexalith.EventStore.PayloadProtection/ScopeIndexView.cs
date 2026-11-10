// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10.2-10.4; owner decision D1 (2026-11-10).
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Head-confirmed scope-index evidence for the pages one operation needs; a complete snapshot converts to a view of every page.</summary>
/// <param name="ScopeDigest">The authenticated scope address.</param>
/// <param name="Head">The confirmed head and ETag, or null for an absent initial index.</param>
/// <param name="Pages">The read pages keyed by page number; a page not read is absent, never empty.</param>
internal sealed record ScopeIndexView(string ScopeDigest, PayloadProtectionStateValue<ScopeIndexHeadV1>? Head,
    IReadOnlyDictionary<uint, PayloadProtectionStateValue<ScopeIndexPageV1>> Pages)
{
    /// <summary>Converts complete scope evidence into a view holding every declared page.</summary>
    public static implicit operator ScopeIndexView(ScopeKeyIndexSnapshot snapshot) => From(snapshot);

    /// <summary>Creates a view holding every page of complete scope evidence.</summary>
    internal static ScopeIndexView From(ScopeKeyIndexSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(snapshot.ScopeDigest, snapshot.Head, new SortedDictionary<uint, PayloadProtectionStateValue<ScopeIndexPageV1>>(
            snapshot.Pages.ToDictionary(page => page.Value.Page)).AsReadOnly());
    }

    /// <summary>Formats no scope or key identities.</summary>
    public override string ToString() => nameof(ScopeIndexView);
}
