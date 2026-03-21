namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// A causation chain tracing the origin and downstream effects of an event.
/// </summary>
/// <param name="OriginatingCommandType">The type name of the command that started the chain.</param>
/// <param name="OriginatingCommandId">The message ID of the originating command.</param>
/// <param name="CorrelationId">The correlation identifier linking all entries in the chain.</param>
/// <param name="UserId">The user who initiated the command, if known.</param>
/// <param name="Events">The events produced in this causation chain.</param>
/// <param name="AffectedProjections">The names of projections affected by this chain.</param>
public record CausationChain(
    string OriginatingCommandType,
    string OriginatingCommandId,
    string CorrelationId,
    string? UserId,
    IReadOnlyList<CausationEvent> Events,
    IReadOnlyList<string> AffectedProjections)
{
    /// <summary>Gets the type name of the command that started the chain.</summary>
    public string OriginatingCommandType { get; } = !string.IsNullOrWhiteSpace(OriginatingCommandType)
        ? OriginatingCommandType
        : throw new ArgumentException("OriginatingCommandType cannot be null, empty, or whitespace.", nameof(OriginatingCommandType));

    /// <summary>Gets the message ID of the originating command.</summary>
    public string OriginatingCommandId { get; } = !string.IsNullOrWhiteSpace(OriginatingCommandId)
        ? OriginatingCommandId
        : throw new ArgumentException("OriginatingCommandId cannot be null, empty, or whitespace.", nameof(OriginatingCommandId));

    /// <summary>Gets the correlation identifier.</summary>
    public string CorrelationId { get; } = !string.IsNullOrWhiteSpace(CorrelationId)
        ? CorrelationId
        : throw new ArgumentException("CorrelationId cannot be null, empty, or whitespace.", nameof(CorrelationId));

    /// <summary>Gets the events produced in this causation chain.</summary>
    public IReadOnlyList<CausationEvent> Events { get; } = Events ?? throw new ArgumentNullException(nameof(Events));

    /// <summary>Gets the names of projections affected by this chain.</summary>
    public IReadOnlyList<string> AffectedProjections { get; } = AffectedProjections ?? throw new ArgumentNullException(nameof(AffectedProjections));
}
