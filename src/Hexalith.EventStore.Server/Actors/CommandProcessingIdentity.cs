namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Identifies one command for pipeline resume and idempotency decisions.
/// </summary>
/// <param name="MessageId">The command message identifier.</param>
/// <param name="CausationId">The normalized causation identifier.</param>
/// <param name="CommandType">The command type name.</param>
public sealed record CommandProcessingIdentity(
    string MessageId,
    string CausationId,
    string CommandType)
{
    /// <summary>
    /// Determines whether persisted identity fields prove an exact match.
    /// </summary>
    /// <param name="messageId">The persisted message identifier.</param>
    /// <param name="causationId">The persisted normalized causation identifier.</param>
    /// <param name="commandType">The persisted command type.</param>
    /// <returns><see langword="true"/> only when all identity fields match ordinally.</returns>
    public bool Matches(string? messageId, string? causationId, string? commandType)
        => string.Equals(MessageId, messageId, StringComparison.Ordinal)
            && string.Equals(CausationId, causationId, StringComparison.Ordinal)
            && string.Equals(CommandType, commandType, StringComparison.Ordinal);

    /// <summary>
    /// Determines whether a pipeline checkpoint belongs to this exact command.
    /// </summary>
    /// <param name="pipelineState">The persisted pipeline checkpoint.</param>
    /// <returns><see langword="true"/> only when all checkpoint identity fields match.</returns>
    public bool Matches(PipelineState pipelineState)
    {
        ArgumentNullException.ThrowIfNull(pipelineState);
        return Matches(pipelineState.MessageId, pipelineState.CausationId, pipelineState.CommandType);
    }

    /// <summary>
    /// Validates that all identity fields are present.
    /// </summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(MessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(CausationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(CommandType);
    }
}
