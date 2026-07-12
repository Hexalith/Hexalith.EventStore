
using System.Xml;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Models;
/// <summary>
/// API response model for command status queries.
/// </summary>
public record CommandStatusResponse(
    string CorrelationId,
    string Status,
    int StatusCode,
    DateTimeOffset Timestamp,
    string? AggregateId,
    int? EventCount,
    string? RejectionEventType,
    string? FailureReason,
    string? TimeoutDuration,
    string? MessageId = null) {
    /// <summary>
    /// Creates a <see cref="CommandStatusResponse"/> from a <see cref="CommandStatusRecord"/>.
    /// </summary>
    public static CommandStatusResponse FromRecord(string statusKey, CommandStatusRecord record) {
        ArgumentException.ThrowIfNullOrWhiteSpace(statusKey);
        ArgumentNullException.ThrowIfNull(record);
        return new CommandStatusResponse(
            CorrelationId: record.CorrelationId ?? statusKey,
            Status: record.Status.ToString(),
            StatusCode: (int)record.Status,
            Timestamp: record.Timestamp,
            AggregateId: record.AggregateId,
            EventCount: record.EventCount,
            RejectionEventType: record.RejectionEventType,
            FailureReason: record.FailureReason,
            TimeoutDuration: record.TimeoutDuration is not null ? XmlConvert.ToString(record.TimeoutDuration.Value) : null,
            MessageId: record.MessageId);
    }
}
