using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>Attests an explicit replay recovery binding without changing the historical route catalog fingerprint.</summary>
/// <param name="Version">The binding protocol version.</param>
/// <param name="CatalogFingerprint">The unchanged authoritative route catalog identity.</param>
/// <param name="BindingFingerprint">The opaque service-owned recovery binding identity.</param>
public sealed record ProjectionReplayRecoveryBinding(int Version, string CatalogFingerprint, string BindingFingerprint)
{
    /// <summary>The separately authenticated rebuild transport header; public JSON carriers remain unchanged.</summary>
    public const string HeaderName = "x-hexalith-projection-replay-recovery";

    /// <summary>Gets whether this exact binding representation is supported.</summary>
    [JsonIgnore]
    public bool IsSupported => Version == 1 && Canonical(CatalogFingerprint) && Canonical(BindingFingerprint);

    private static bool Canonical(string? value) => value is { Length: 64 }
        && value.All(static item => item is >= '0' and <= '9' or >= 'a' and <= 'f');
}
