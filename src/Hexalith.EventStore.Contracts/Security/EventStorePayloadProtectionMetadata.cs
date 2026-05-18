using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Provider-neutral protection metadata describing how an event payload or snapshot state was
/// transformed. The shape is intentionally minimal and non-secret: it identifies state, scheme,
/// version, and safe references without ever embedding raw keys, plaintext, IVs/nonces,
/// authentication tags, or provider-private blobs.
/// </summary>
/// <param name="State">The protection state (unprotected, protected, or provider-opaque).</param>
/// <param name="MetadataVersion">The metadata schema version (must be &gt;= 1). Current schema version is 1.</param>
/// <param name="Scheme">Optional provider/scheme family identifier (e.g. <c>"aes-gcm-256"</c>). Must be non-secret.</param>
/// <param name="KeyAlias">Optional non-secret key reference. Treated sensitive-by-default by callers.</param>
/// <param name="ContentHint">Optional content-type hint after un/protection (e.g. <c>"application/json"</c>).</param>
/// <param name="CompatibilityFlags">Optional small bounded forward-compatibility flag dictionary.</param>
public sealed record EventStorePayloadProtectionMetadata(
    PayloadProtectionState State,
    int MetadataVersion,
    string? Scheme,
    string? KeyAlias,
    string? ContentHint,
    IReadOnlyDictionary<string, string>? CompatibilityFlags) {
    /// <summary>The current metadata schema version emitted by this Hexalith.EventStore release.</summary>
    public const int CurrentMetadataVersion = 1;

    /// <summary>Max length for the <see cref="Scheme"/> field (printable ASCII).</summary>
    public const int MaxSchemeLength = 64;

    /// <summary>Max length for the <see cref="KeyAlias"/> field (printable ASCII).</summary>
    public const int MaxKeyAliasLength = 256;

    /// <summary>Max length for the <see cref="ContentHint"/> field (printable ASCII).</summary>
    public const int MaxContentHintLength = 128;

    /// <summary>Max number of entries allowed in <see cref="CompatibilityFlags"/>.</summary>
    public const int MaxCompatibilityFlagCount = 8;

    /// <summary>Max key length for a <see cref="CompatibilityFlags"/> entry.</summary>
    public const int MaxCompatibilityFlagKeyLength = 64;

    /// <summary>Max value length for a <see cref="CompatibilityFlags"/> entry.</summary>
    public const int MaxCompatibilityFlagValueLength = 256;

    /// <summary>
    /// Returns a no-op metadata record for the supplied state. Useful for the default no-op
    /// protection provider and for legacy compatibility mapping.
    /// </summary>
    /// <param name="state">The protection state. Defaults to <see cref="PayloadProtectionState.Unprotected"/>.</param>
    /// <returns>A metadata record at the current schema version with no scheme, key alias, or hint.</returns>
    public static EventStorePayloadProtectionMetadata Unprotected(PayloadProtectionState state = PayloadProtectionState.Unprotected)
        => new(state, CurrentMetadataVersion, null, null, null, null);

    /// <summary>
    /// Returns a metadata record describing a value EventStore cannot interpret. Used by the
    /// fail-closed path for malformed or unknown-version metadata.
    /// </summary>
    /// <param name="reason">Optional short reason flag merged into <see cref="CompatibilityFlags"/>.</param>
    /// <returns>A <see cref="PayloadProtectionState.ProviderOpaque"/> metadata record.</returns>
    public static EventStorePayloadProtectionMetadata ProviderOpaque(string? reason = null) {
        IReadOnlyDictionary<string, string>? flags = null;
        if (!string.IsNullOrWhiteSpace(reason)) {
            flags = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(System.StringComparer.Ordinal) { ["reason"] = reason });
        }

        return new EventStorePayloadProtectionMetadata(PayloadProtectionState.ProviderOpaque, CurrentMetadataVersion, null, null, null, flags);
    }

    /// <inheritdoc/>
    public bool Equals(EventStorePayloadProtectionMetadata? other) {
        if (other is null) {
            return false;
        }

        if (State != other.State
            || MetadataVersion != other.MetadataVersion
            || !string.Equals(Scheme, other.Scheme, System.StringComparison.Ordinal)
            || !string.Equals(KeyAlias, other.KeyAlias, System.StringComparison.Ordinal)
            || !string.Equals(ContentHint, other.ContentHint, System.StringComparison.Ordinal)) {
            return false;
        }

        if (CompatibilityFlags is null && other.CompatibilityFlags is null) {
            return true;
        }

        if (CompatibilityFlags is null || other.CompatibilityFlags is null) {
            return false;
        }

        if (CompatibilityFlags.Count != other.CompatibilityFlags.Count) {
            return false;
        }

        foreach (KeyValuePair<string, string> entry in CompatibilityFlags) {
            if (!other.CompatibilityFlags.TryGetValue(entry.Key, out string? otherValue)
                || !string.Equals(entry.Value, otherValue, System.StringComparison.Ordinal)) {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        var hash = new System.HashCode();
        hash.Add(State);
        hash.Add(MetadataVersion);
        hash.Add(Scheme, System.StringComparer.Ordinal);
        hash.Add(KeyAlias, System.StringComparer.Ordinal);
        hash.Add(ContentHint, System.StringComparer.Ordinal);
        if (CompatibilityFlags is not null) {
            foreach (KeyValuePair<string, string> entry in CompatibilityFlags.OrderBy(static e => e.Key, System.StringComparer.Ordinal)) {
                hash.Add(entry.Key, System.StringComparer.Ordinal);
                hash.Add(entry.Value, System.StringComparer.Ordinal);
            }
        }

        return hash.ToHashCode();
    }
}
