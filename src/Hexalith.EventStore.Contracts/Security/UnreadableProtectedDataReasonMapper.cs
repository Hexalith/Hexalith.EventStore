using System;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Maps protection metadata states and compatibility flags to the public unreadable-protected-data
/// taxonomy. Keeps malformed, unknown-version, and provider-opaque cases consistent across runtime
/// surfaces.
/// </summary>
public static class UnreadableProtectedDataReasonMapper {
    /// <summary>
    /// Maps provider-opaque metadata to the closest unreadable reason category.
    /// </summary>
    /// <param name="metadata">The provider-opaque metadata.</param>
    /// <returns>The unreadable reason category.</returns>
    public static UnreadableProtectedDataReason FromProviderOpaqueMetadata(EventStorePayloadProtectionMetadata metadata) {
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.State != PayloadProtectionState.ProviderOpaque) {
            throw new ArgumentException("Metadata must be provider-opaque.", nameof(metadata));
        }

        string? reason = metadata.CompatibilityFlags is not null
            && metadata.CompatibilityFlags.TryGetValue("reason", out string? value)
                ? value
                : null;

        return reason switch {
            "unknownVersion" => UnreadableProtectedDataReason.UnknownMetadataVersion,
            "parseError" or "unknownState" or "forbidden" or "unknownField" => UnreadableProtectedDataReason.MalformedMetadata,
            _ => UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation,
        };
    }
}
