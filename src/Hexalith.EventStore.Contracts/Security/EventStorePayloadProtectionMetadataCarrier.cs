using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Provides centralized read/write/validate helpers for protection metadata stored in
/// EventStore-owned envelope extension dictionaries. All metadata interpretation (legacy,
/// malformed, unknown-version, forbidden-shape) goes through this carrier so every caller gets
/// the same deterministic state-machine semantics defined in Story 22.7a.
/// </summary>
public static class EventStorePayloadProtectionMetadataCarrier {
    /// <summary>
    /// The reserved EventStore-owned extension key under which protection metadata is serialized.
    /// </summary>
    public const string ExtensionKey = "eventstore.protection";

    private static readonly string[] _forbiddenSubstrings = [
        "password",
        "secret",
        "private-key",
        "privatekey",
        "connection-string",
        "connectionstring",
        "plaintext",
        "dapr-secret",
        "vault-uri",
        "bearer ",
    ];

    private static readonly string[] _forbiddenKeyNames = [
        "key",
        "iv",
        "nonce",
        "tag",
        "auth-tag",
        "authtag",
        "cipher",
    ];

    private static readonly JsonSerializerOptions _jsonOptions = new() {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>
    /// Returns <see langword="true"/> when the supplied metadata satisfies every validation rule
    /// for safe serialization (length bounds, ASCII printable, no forbidden secret-shaped fields,
    /// no unknown future version).
    /// </summary>
    /// <param name="metadata">The metadata to validate. May be <see langword="null"/>.</param>
    /// <param name="rejectionReason">When the result is <see langword="false"/>, a short human-readable reason.</param>
    /// <returns><see langword="true"/> when the metadata is safe to serialize.</returns>
    public static bool TryValidate(EventStorePayloadProtectionMetadata? metadata, out string? rejectionReason) {
        if (metadata is null) {
            rejectionReason = "Metadata is null.";
            return false;
        }

        if (metadata.MetadataVersion < 1 || metadata.MetadataVersion > EventStorePayloadProtectionMetadata.CurrentMetadataVersion) {
            rejectionReason = $"MetadataVersion {metadata.MetadataVersion} is unsupported (must be 1..{EventStorePayloadProtectionMetadata.CurrentMetadataVersion}).";
            return false;
        }

        if (metadata.Scheme is not null) {
            if (metadata.Scheme.Length > EventStorePayloadProtectionMetadata.MaxSchemeLength) {
                rejectionReason = "Scheme exceeds maximum length.";
                return false;
            }

            if (!IsAsciiPrintable(metadata.Scheme)) {
                rejectionReason = "Scheme must be ASCII-printable.";
                return false;
            }

            if (ContainsForbiddenSubstring(metadata.Scheme)) {
                rejectionReason = "Scheme contains a forbidden secret-shaped substring.";
                return false;
            }
        }

        if (metadata.KeyAlias is not null) {
            if (metadata.KeyAlias.Length > EventStorePayloadProtectionMetadata.MaxKeyAliasLength) {
                rejectionReason = "KeyAlias exceeds maximum length.";
                return false;
            }

            if (!IsAsciiPrintable(metadata.KeyAlias)) {
                rejectionReason = "KeyAlias must be ASCII-printable.";
                return false;
            }

            if (ContainsForbiddenSubstring(metadata.KeyAlias)) {
                rejectionReason = "KeyAlias contains a forbidden secret-shaped substring.";
                return false;
            }
        }

        if (metadata.ContentHint is not null) {
            if (metadata.ContentHint.Length > EventStorePayloadProtectionMetadata.MaxContentHintLength) {
                rejectionReason = "ContentHint exceeds maximum length.";
                return false;
            }

            if (!IsAsciiPrintable(metadata.ContentHint)) {
                rejectionReason = "ContentHint must be ASCII-printable.";
                return false;
            }
        }

        if (metadata.CompatibilityFlags is not null) {
            if (metadata.CompatibilityFlags.Count > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagCount) {
                rejectionReason = "CompatibilityFlags exceeds maximum entry count.";
                return false;
            }

            foreach (KeyValuePair<string, string> entry in metadata.CompatibilityFlags) {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagKeyLength) {
                    rejectionReason = "CompatibilityFlags key is invalid.";
                    return false;
                }

                if (!IsAsciiPrintable(entry.Key) || IsForbiddenKeyName(entry.Key) || ContainsForbiddenSubstring(entry.Key)) {
                    rejectionReason = "CompatibilityFlags key contains a forbidden secret-shaped value.";
                    return false;
                }

                if (entry.Value is null || entry.Value.Length > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagValueLength) {
                    rejectionReason = "CompatibilityFlags value is invalid.";
                    return false;
                }

                if (!IsAsciiPrintable(entry.Value) || ContainsForbiddenSubstring(entry.Value)) {
                    rejectionReason = "CompatibilityFlags value contains a forbidden secret-shaped substring.";
                    return false;
                }
            }
        }

        // State invariants
        if (metadata.State == PayloadProtectionState.Protected && string.IsNullOrWhiteSpace(metadata.Scheme)) {
            rejectionReason = "Protected state requires a non-empty Scheme.";
            return false;
        }

        rejectionReason = null;
        return true;
    }

    /// <summary>
    /// Serializes the supplied metadata to a UTF-8 JSON string. Throws when the metadata fails
    /// validation, so callers must call <see cref="TryValidate"/> first when they accept input
    /// from external providers.
    /// </summary>
    /// <param name="metadata">The metadata to serialize.</param>
    /// <returns>A UTF-8 JSON string suitable for storage in an envelope extension dictionary.</returns>
    /// <exception cref="ArgumentException">Thrown when the metadata fails validation.</exception>
    public static string Serialize(EventStorePayloadProtectionMetadata metadata) {
        ArgumentNullException.ThrowIfNull(metadata);
        if (!TryValidate(metadata, out string? reason)) {
            throw new ArgumentException(reason, nameof(metadata));
        }

        var dto = new ProtectionMetadataDto(
            State: metadata.State.ToString(),
            MetadataVersion: metadata.MetadataVersion,
            Scheme: metadata.Scheme,
            KeyAlias: metadata.KeyAlias,
            ContentHint: metadata.ContentHint,
            CompatibilityFlags: metadata.CompatibilityFlags is null
                ? null
                : new Dictionary<string, string>(metadata.CompatibilityFlags, StringComparer.Ordinal));

        return JsonSerializer.Serialize(dto, _jsonOptions);
    }

    /// <summary>
    /// Tries to deserialize the supplied serialized metadata payload. Malformed payloads, unknown
    /// future schema versions, and forbidden secret-shaped fields all map to
    /// <see cref="EventStorePayloadProtectionMetadata.ProviderOpaque"/> instead of being inferred
    /// as safe.
    /// </summary>
    /// <param name="serialized">The serialized metadata. May be <see langword="null"/> or empty (legacy).</param>
    /// <returns>The parsed metadata, the legacy compatibility record, or the provider-opaque fallback.</returns>
    public static EventStorePayloadProtectionMetadata Read(string? serialized) {
        if (string.IsNullOrWhiteSpace(serialized)) {
            return Legacy();
        }

        JsonDocument document;
        try {
            document = JsonDocument.Parse(serialized);
        }
        catch (JsonException) {
            return EventStorePayloadProtectionMetadata.ProviderOpaque("parseError");
        }

        using (document) {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) {
                return EventStorePayloadProtectionMetadata.ProviderOpaque("parseError");
            }

            EventStorePayloadProtectionMetadata? unknownFieldResult = CheckForUnknownFields(root);
            if (unknownFieldResult is not null) {
                return unknownFieldResult;
            }

            ProtectionMetadataDto? dto;
            try {
                dto = root.Deserialize<ProtectionMetadataDto>(_jsonOptions);
            }
            catch (JsonException) {
                return EventStorePayloadProtectionMetadata.ProviderOpaque("parseError");
            }

            if (dto is null || string.IsNullOrWhiteSpace(dto.State)) {
                return EventStorePayloadProtectionMetadata.ProviderOpaque("parseError");
            }

            if (!Enum.TryParse(dto.State, ignoreCase: false, out PayloadProtectionState state)) {
                return EventStorePayloadProtectionMetadata.ProviderOpaque("unknownState");
            }

            if (dto.MetadataVersion < 1 || dto.MetadataVersion > EventStorePayloadProtectionMetadata.CurrentMetadataVersion) {
                return EventStorePayloadProtectionMetadata.ProviderOpaque("unknownVersion");
            }

            IReadOnlyDictionary<string, string>? flags = dto.CompatibilityFlags is null
                ? null
                : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(dto.CompatibilityFlags, StringComparer.Ordinal));

            var candidate = new EventStorePayloadProtectionMetadata(
                state,
                dto.MetadataVersion,
                dto.Scheme,
                dto.KeyAlias,
                dto.ContentHint,
                flags);

            if (!TryValidate(candidate, out _)) {
                return EventStorePayloadProtectionMetadata.ProviderOpaque("forbidden");
            }

            return candidate;
        }
    }

    /// <summary>
    /// Reads the protection metadata recorded inside the supplied extension dictionary, or returns
    /// the legacy compatibility record when the dictionary is missing or has no protection entry.
    /// </summary>
    /// <param name="extensions">An envelope extension dictionary (may be <see langword="null"/>).</param>
    /// <returns>The interpreted protection metadata.</returns>
    public static EventStorePayloadProtectionMetadata Read(IReadOnlyDictionary<string, string>? extensions) {
        if (extensions is null || !extensions.TryGetValue(ExtensionKey, out string? serialized)) {
            return Legacy();
        }

        return Read(serialized);
    }

    /// <summary>
    /// Reads the protection metadata recorded inside the supplied mutable extension dictionary, or
    /// returns the legacy compatibility record when the dictionary is missing or has no protection
    /// entry.
    /// </summary>
    /// <param name="extensions">An envelope extension dictionary (may be <see langword="null"/>).</param>
    /// <returns>The interpreted protection metadata.</returns>
    public static EventStorePayloadProtectionMetadata Read(IDictionary<string, string>? extensions) {
        if (extensions is null || !extensions.TryGetValue(ExtensionKey, out string? serialized)) {
            return Legacy();
        }

        return Read(serialized);
    }

    /// <summary>
    /// Returns a new extension dictionary with the protection metadata key set to the supplied
    /// value. Other keys are preserved.
    /// </summary>
    /// <param name="extensions">The existing extension dictionary, or <see langword="null"/>.</param>
    /// <param name="metadata">The metadata to write.</param>
    /// <returns>A new mutable dictionary suitable for storage on a server <c>EventEnvelope</c>.</returns>
    public static IDictionary<string, string> Write(IDictionary<string, string>? extensions, EventStorePayloadProtectionMetadata metadata) {
        ArgumentNullException.ThrowIfNull(metadata);
        string serialized = Serialize(metadata);
        Dictionary<string, string> copy = extensions is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(extensions, StringComparer.Ordinal);
        copy[ExtensionKey] = serialized;
        return copy;
    }

    /// <summary>
    /// Returns a new read-only extension dictionary with the protection metadata key set to the
    /// supplied value. Other keys are preserved.
    /// </summary>
    /// <param name="extensions">The existing extension dictionary, or <see langword="null"/>.</param>
    /// <param name="metadata">The metadata to write.</param>
    /// <returns>A new read-only dictionary suitable for contract <c>EventEnvelope</c> consumers.</returns>
    public static IReadOnlyDictionary<string, string> Write(IReadOnlyDictionary<string, string>? extensions, EventStorePayloadProtectionMetadata metadata) {
        ArgumentNullException.ThrowIfNull(metadata);
        string serialized = Serialize(metadata);
        Dictionary<string, string> copy = extensions is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(extensions, StringComparer.Ordinal);
        copy[ExtensionKey] = serialized;
        return new ReadOnlyDictionary<string, string>(copy);
    }

    /// <summary>
    /// Returns the legacy compatibility record used when an envelope predates Story 22.7a and
    /// carries no protection metadata at all.
    /// </summary>
    /// <returns>An <see cref="PayloadProtectionState.Unprotected"/> record flagged as legacy.</returns>
    public static EventStorePayloadProtectionMetadata Legacy() {
        var flags = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal) { ["legacy"] = "missing" });
        return new EventStorePayloadProtectionMetadata(
            PayloadProtectionState.Unprotected,
            EventStorePayloadProtectionMetadata.CurrentMetadataVersion,
            Scheme: null,
            KeyAlias: null,
            ContentHint: null,
            CompatibilityFlags: flags);
    }

    private static bool IsAsciiPrintable(string value) {
        foreach (char c in value) {
            if (c < 0x20 || c > 0x7E) {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsForbiddenSubstring(string value) {
        string lowered = value.ToLowerInvariant();
        return _forbiddenSubstrings.Any(forbidden => lowered.Contains(forbidden, StringComparison.Ordinal));
    }

    private static bool IsForbiddenKeyName(string key) {
        string lowered = key.ToLowerInvariant();
        return _forbiddenKeyNames.Contains(lowered, StringComparer.Ordinal);
    }

    private static EventStorePayloadProtectionMetadata? CheckForUnknownFields(JsonElement root) {
        foreach (JsonProperty property in root.EnumerateObject()) {
            if (IsKnownTopLevelProperty(property.Name)) {
                continue;
            }

            return IsForbiddenKeyName(property.Name)
                || ContainsForbiddenSubstring(property.Name)
                || ContainsForbiddenJsonString(property.Value)
                ? EventStorePayloadProtectionMetadata.ProviderOpaque("forbidden")
                : EventStorePayloadProtectionMetadata.ProviderOpaque("unknownField");
        }

        return null;
    }

    private static bool IsKnownTopLevelProperty(string propertyName)
        => string.Equals(propertyName, "state", StringComparison.Ordinal)
        || string.Equals(propertyName, "metadataVersion", StringComparison.Ordinal)
        || string.Equals(propertyName, "scheme", StringComparison.Ordinal)
        || string.Equals(propertyName, "keyAlias", StringComparison.Ordinal)
        || string.Equals(propertyName, "contentHint", StringComparison.Ordinal)
        || string.Equals(propertyName, "compatibilityFlags", StringComparison.Ordinal);

    private static bool ContainsForbiddenJsonString(JsonElement element) {
        switch (element.ValueKind) {
            case JsonValueKind.String:
                return ContainsForbiddenSubstring(element.GetString() ?? string.Empty);
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject()) {
                    if (ContainsForbiddenSubstring(property.Name) || ContainsForbiddenJsonString(property.Value)) {
                        return true;
                    }
                }

                return false;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray()) {
                    if (ContainsForbiddenJsonString(item)) {
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }

    private sealed record ProtectionMetadataDto(
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("metadataVersion")] int MetadataVersion,
        [property: JsonPropertyName("scheme")] string? Scheme,
        [property: JsonPropertyName("keyAlias")] string? KeyAlias,
        [property: JsonPropertyName("contentHint")] string? ContentHint,
        [property: JsonPropertyName("compatibilityFlags")] Dictionary<string, string>? CompatibilityFlags);
}
