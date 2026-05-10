using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Source attribution for a DAPR component fact. Lets the UI explain which sidecar or probe
/// supplied the evidence for a component row.
/// </summary>
/// <remarks>
/// <para>Default of <see cref="Unavailable"/> preserves the truth contract: an absent source
/// must never render as healthy or available evidence. The default also keeps wire-format
/// compatibility for legacy clients that do not yet read the field.</para>
/// <para>Ordinals are pinned explicitly so the wire format is stable across versions
/// and external clients. New members MUST be appended with the next unused ordinal;
/// existing members MUST NOT be reordered or renumbered. The
/// <c>DaprHealthHistoryCollector</c> skip rule uses a positive whitelist of
/// known-non-<see cref="Unavailable"/> values, so new additions default to "no source
/// attribution" until they are explicitly added to the whitelist.</para>
/// <para>Serializes by name via <see cref="JsonStringEnumConverter"/> regardless of
/// caller configuration.</para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DaprComponentSource {
    /// <summary>The source is unknown or could not be determined.</summary>
    Unavailable = 0,

    /// <summary>Component was reported by the remote EventStore sidecar metadata.</summary>
    RemoteEventStoreMetadata = 1,

    /// <summary>Component was confirmed by a local Admin.Server dependency probe (e.g. state store).</summary>
    LocalAdminProbe = 2,

    /// <summary>Component was sourced from local Admin sidecar metadata as a degraded fallback only.</summary>
    LocalAdminMetadataFallback = 3,
}
