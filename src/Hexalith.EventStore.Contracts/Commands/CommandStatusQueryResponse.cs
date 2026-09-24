namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// Client-facing shape of a command status read (<c>GET api/v1/commands/status/{messageId}</c>). It carries the
/// terminal outcome evidence a caller needs to tell a domain rejection apart from an unverifiable submission, and
/// deliberately omits the infrastructure detail the API response also exposes.
/// </summary>
/// <param name="CorrelationId">The tracing correlation identifier the status was recorded under.</param>
/// <param name="Status">The command lifecycle status name, matching <see cref="CommandStatus"/>.</param>
/// <param name="StatusCode">The ordinal form of <paramref name="Status"/>.</param>
/// <param name="RejectionEventType">The rejection event type name when the command was domain-rejected.</param>
/// <param name="MessageId">The command message identifier, or <c>null</c> for a legacy record.</param>
public sealed record CommandStatusQueryResponse(
    string CorrelationId,
    string Status,
    int StatusCode,
    string? RejectionEventType = null,
    string? MessageId = null)
{
    /// <summary>Gets the number of events produced by a completed command; zero identifies an authoritative no-op.</summary>
    public int? EventCount { get; init; }

    /// <summary>Gets the tenant scope that owns this command status.</summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Gets the bounded failure reason when the rejection or terminal failure was infrastructural.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Gets a value indicating whether the command reached a canonical domain-rejected terminal state.
    /// Infrastructure rejections use the same lifecycle status but carry <see cref="FailureReason"/> instead of a
    /// rejection event type and therefore return <see langword="false"/>.
    /// </summary>
    public bool IsRejected
        => StatusCode == (int)CommandStatus.Rejected
            && string.Equals(Status, nameof(CommandStatus.Rejected), StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(RejectionEventType)
            && string.IsNullOrWhiteSpace(FailureReason)
            && Retryable is not true;

    /// <summary>
    /// Gets whether the platform has armed an automatic retry, or <c>null</c> for a legacy record that carries no
    /// recovery signal.
    /// </summary>
    public bool? Retryable { get; init; }
}
