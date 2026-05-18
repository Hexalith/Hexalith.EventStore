namespace Hexalith.EventStore.Admin.Abstractions.Models;

/// <summary>
/// Admin-facing descriptor for content that is intentionally redacted before serialization or rendering.
/// </summary>
/// <param name="IsRedacted">Whether the content was redacted.</param>
/// <param name="ContentKind">The kind of content that was redacted.</param>
/// <param name="Placeholder">Deterministic placeholder text for operators.</param>
/// <param name="ReasonCode">Stable machine-readable reason code.</param>
/// <param name="Stage">Processing or presentation stage where the decision was made.</param>
/// <param name="MetadataVersion">Protection metadata version when available.</param>
/// <param name="Retryable">Whether retrying may succeed.</param>
/// <param name="Permanent">Whether the condition is permanent.</param>
/// <param name="SafeNextAction">Operator-safe guidance.</param>
/// <param name="TenantId">Safe tenant identifier when available.</param>
/// <param name="Domain">Safe domain name when available.</param>
/// <param name="AggregateId">Safe aggregate identifier when available.</param>
/// <param name="SequenceNumber">Safe sequence number when available.</param>
/// <param name="CorrelationId">Safe correlation identifier when available.</param>
public sealed record AdminRedactedContent(
    bool IsRedacted,
    string ContentKind,
    string Placeholder,
    string ReasonCode,
    string Stage,
    int? MetadataVersion,
    bool Retryable,
    bool Permanent,
    string SafeNextAction,
    string? TenantId = null,
    string? Domain = null,
    string? AggregateId = null,
    long? SequenceNumber = null,
    string? CorrelationId = null) {
    /// <summary>Default deterministic placeholder for protected Admin content.</summary>
    public const string DefaultPlaceholder = "Protected content redacted.";

    /// <summary>Creates a protected-content descriptor from safe Admin metadata.</summary>
    public static AdminRedactedContent Protected(
        string contentKind,
        string reasonCode,
        string stage,
        int? metadataVersion,
        bool retryable,
        bool permanent,
        string safeNextAction,
        string? tenantId = null,
        string? domain = null,
        string? aggregateId = null,
        long? sequenceNumber = null,
        string? correlationId = null)
        => new(
            IsRedacted: true,
            ContentKind: Require(contentKind, nameof(contentKind)),
            Placeholder: DefaultPlaceholder,
            ReasonCode: Require(reasonCode, nameof(reasonCode)),
            Stage: Require(stage, nameof(stage)),
            MetadataVersion: metadataVersion,
            Retryable: retryable,
            Permanent: permanent,
            SafeNextAction: Require(safeNextAction, nameof(safeNextAction)),
            TenantId: tenantId,
            Domain: domain,
            AggregateId: aggregateId,
            SequenceNumber: sequenceNumber,
            CorrelationId: correlationId);

    /// <inheritdoc/>
    public override string ToString()
        => $"AdminRedactedContent {{ ContentKind = {ContentKind}, ReasonCode = {ReasonCode}, Stage = {Stage}, IsRedacted = {IsRedacted} }}";

    private static string Require(string value, string paramName)
        => !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"{paramName} cannot be null, empty, or whitespace.", paramName);
}
