using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Pipeline.Commands;

/// <summary>
/// Result of processing a <see cref="SubmitCommand"/>.
/// </summary>
public record SubmitCommandResult(
    string CorrelationId,
    string? ResultPayload = null,
    string? MessageId = null)
{
    /// <summary>Explains the absence of a result payload when command state is known.</summary>
    public ResultPayloadOmissionReason? ResultPayloadOmissionReason { get; init; }
}
