using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Contracts.Problems;

/// <summary>
/// Stable ProblemDetails contract for unreadable protected data conditions (Story 22.7b). The
/// extension keys here MUST be the only sources of detail emitted in API or admin responses; payload
/// bytes, snapshot state, key material, provider-private metadata, and stack traces never appear.
/// </summary>
public static class UnreadableProtectedDataProblem {
    /// <summary>Stable ProblemDetails type URI for unreadable protected data.</summary>
    public const string TypeUri = "https://hexalith.io/problems/unreadable-protected-data";

    /// <summary>Default human-readable title for unreadable protected data responses.</summary>
    public const string DefaultTitle = "Protected data is unreadable";

    /// <summary>Extension key carrying the unreadable reason category name.</summary>
    public const string ExtensionReasonCategory = "reasonCategory";

    /// <summary>Extension key carrying the pipeline stage where the condition was detected.</summary>
    public const string ExtensionStage = "stage";

    /// <summary>Extension key carrying the affected event sequence number.</summary>
    public const string ExtensionSequenceNumber = "sequenceNumber";

    /// <summary>Extension key carrying the affected projection rebuild checkpoint identifier.</summary>
    public const string ExtensionCheckpointId = "checkpointId";

    /// <summary>Extension key carrying the affected domain.</summary>
    public const string ExtensionDomain = "domain";

    /// <summary>Extension key carrying the affected aggregate identifier.</summary>
    public const string ExtensionAggregateId = "aggregateId";

    /// <summary>Extension key carrying the protection metadata schema version (informational).</summary>
    public const string ExtensionMetadataVersion = "metadataVersion";

    /// <summary>Extension key indicating whether retry may succeed.</summary>
    public const string ExtensionRetryable = "retryable";

    /// <summary>Extension key indicating whether the condition is permanent.</summary>
    public const string ExtensionPermanent = "permanent";

    /// <summary>
    /// Returns the HTTP status code policy associated with an unreadable reason category. Default
    /// is <c>422 Unprocessable Entity</c>. <see cref="UnreadableProtectedDataReason.ProviderUnavailable"/>
    /// returns <c>503</c>; <see cref="UnreadableProtectedDataReason.KeyInvalidatedOrDeleted"/>
    /// returns <c>410</c>.
    /// </summary>
    /// <param name="reason">The unreadable reason category.</param>
    /// <returns>The recommended HTTP status code.</returns>
    public static int GetStatusCode(UnreadableProtectedDataReason reason) => reason switch {
        UnreadableProtectedDataReason.ProviderUnavailable => 503,
        UnreadableProtectedDataReason.KeyInvalidatedOrDeleted => 410,
        _ => 422,
    };

    /// <summary>
    /// Returns a safe operator-facing guidance string. No provider exception text, payload bytes,
    /// key aliases, or stack traces are referenced.
    /// </summary>
    /// <param name="reason">The unreadable reason category.</param>
    /// <returns>A safe, fixed guidance string.</returns>
    public static string GetSafeOperatorGuidance(UnreadableProtectedDataReason reason) => reason switch {
        UnreadableProtectedDataReason.MissingKey =>
            "Required protection key is unavailable. Verify the key is registered with the configured provider.",
        UnreadableProtectedDataReason.KeyInvalidatedOrDeleted =>
            "The protection key has been invalidated or deleted. Stored data is permanently unreadable.",
        UnreadableProtectedDataReason.ProviderUnavailable =>
            "Protection provider is temporarily unavailable. Retry later with backoff.",
        UnreadableProtectedDataReason.ProviderDenied =>
            "Protection provider denied the operation. Verify caller permissions or provider policy.",
        UnreadableProtectedDataReason.ConsistencyMismatch =>
            "Stored bytes and protection metadata are inconsistent. Investigate stream provenance.",
        UnreadableProtectedDataReason.MalformedMetadata =>
            "Protection metadata could not be parsed. Investigate storage integrity.",
        UnreadableProtectedDataReason.UnknownMetadataVersion =>
            "Protection metadata version is unsupported by this EventStore release. Upgrade EventStore.",
        UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation =>
            "Protection metadata is provider-opaque. The requested operation requires interpretable data.",
        UnreadableProtectedDataReason.BytesMetadataMismatch =>
            "Stored bytes and protection metadata state do not agree. Investigate provenance.",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown UnreadableProtectedDataReason value."),
    };
}
