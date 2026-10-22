using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Commands;

/// <summary>
/// Summary information about a command processed by the event store.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="CorrelationId">The correlation identifier linking command to its events.</param>
/// <param name="CommandType">The fully-qualified command type name.</param>
/// <param name="Status">The current command lifecycle status.</param>
/// <param name="Timestamp">When the command was received.</param>
/// <param name="EventCount">Number of events produced (null if not yet completed).</param>
/// <param name="FailureReason">Reason for failure (null if not failed).</param>
public record CommandSummary(
    string TenantId,
    string Domain,
    string AggregateId,
    string CorrelationId,
    string CommandType,
    CommandStatus Status,
    DateTimeOffset Timestamp,
    int? EventCount,
    string? FailureReason) {
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
}
