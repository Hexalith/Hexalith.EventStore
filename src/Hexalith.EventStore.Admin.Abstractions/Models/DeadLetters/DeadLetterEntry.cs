namespace Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;

/// <summary>
/// A dead-letter queue entry representing a failed command.
/// </summary>
/// <param name="MessageId">The unique message identifier.</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="CorrelationId">The correlation identifier for request tracing.</param>
/// <param name="FailureReason">The reason the message was dead-lettered.</param>
/// <param name="FailedAtUtc">When the message was dead-lettered.</param>
/// <param name="RetryCount">The number of times the message has been retried.</param>
/// <param name="OriginalCommandType">The fully qualified type name of the original command.</param>
public record DeadLetterEntry(
    string MessageId,
    string TenantId,
    string Domain,
    string AggregateId,
    string CorrelationId,
    string FailureReason,
    DateTimeOffset FailedAtUtc,
    int RetryCount,
    string OriginalCommandType)
{
    /// <summary>Gets the unique message identifier.</summary>
    public string MessageId { get; } = !string.IsNullOrWhiteSpace(MessageId)
        ? MessageId
        : throw new ArgumentException("MessageId cannot be null, empty, or whitespace.", nameof(MessageId));

    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = !string.IsNullOrWhiteSpace(TenantId)
        ? TenantId
        : throw new ArgumentException("TenantId cannot be null, empty, or whitespace.", nameof(TenantId));

    /// <summary>Gets the domain name.</summary>
    public string Domain { get; } = !string.IsNullOrWhiteSpace(Domain)
        ? Domain
        : throw new ArgumentException("Domain cannot be null, empty, or whitespace.", nameof(Domain));

    /// <summary>Gets the aggregate identifier.</summary>
    public string AggregateId { get; } = !string.IsNullOrWhiteSpace(AggregateId)
        ? AggregateId
        : throw new ArgumentException("AggregateId cannot be null, empty, or whitespace.", nameof(AggregateId));

    /// <summary>Gets the correlation identifier.</summary>
    public string CorrelationId { get; } = !string.IsNullOrWhiteSpace(CorrelationId)
        ? CorrelationId
        : throw new ArgumentException("CorrelationId cannot be null, empty, or whitespace.", nameof(CorrelationId));

    /// <summary>Gets the failure reason.</summary>
    public string FailureReason { get; } = !string.IsNullOrWhiteSpace(FailureReason)
        ? FailureReason
        : throw new ArgumentException("FailureReason cannot be null, empty, or whitespace.", nameof(FailureReason));

    /// <summary>Gets the original command type.</summary>
    public string OriginalCommandType { get; } = !string.IsNullOrWhiteSpace(OriginalCommandType)
        ? OriginalCommandType
        : throw new ArgumentException("OriginalCommandType cannot be null, empty, or whitespace.", nameof(OriginalCommandType));

    /// <summary>
    /// Returns a string representation with failure details redacted (SEC-5).
    /// </summary>
    public override string ToString()
        => $"DeadLetterEntry {{ MessageId = {MessageId}, TenantId = {TenantId}, Domain = {Domain}, AggregateId = {AggregateId}, CorrelationId = {CorrelationId}, FailureReason = [REDACTED], FailedAtUtc = {FailedAtUtc}, RetryCount = {RetryCount}, OriginalCommandType = {OriginalCommandType} }}";
}
