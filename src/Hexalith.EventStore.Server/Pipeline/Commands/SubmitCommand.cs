
using MediatR;

namespace Hexalith.EventStore.Server.Pipeline.Commands;
/// <summary>
/// MediatR command for submitting a domain command through the pipeline.
/// </summary>
public record SubmitCommand(
    string MessageId,
    string Tenant,
    string Domain,
    string AggregateId,
    string CommandType,
    byte[] Payload,
    string CorrelationId,
    string UserId,
    Dictionary<string, string>? Extensions = null) : IRequest<SubmitCommandResult>;

/// <summary>
/// Result of processing a <see cref="SubmitCommand"/>.
/// </summary>
public record SubmitCommandResult(string CorrelationId);
