namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// Record for storing original command data for replay.
/// </summary>
/// <remarks>
/// Note: This record contains <c>byte[] Payload</c> and <c>Dictionary&lt;string, string&gt;? Extensions</c>,
/// which use reference equality. Record value-based equality (Equals/GetHashCode) will NOT compare
/// these members by content. Avoid relying on record equality for ArchivedCommand instances;
/// compare individual fields when needed.
/// </remarks>
/// <param name="Tenant">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="CommandType">The command type.</param>
/// <param name="Payload">The serialized command payload.</param>
/// <param name="Extensions">Optional command extensions.</param>
/// <param name="OriginalTimestamp">When the original command was archived.</param>
/// <param name="MessageId">The original message identifier, or <c>null</c> for a legacy archive.</param>
/// <param name="CorrelationId">The original correlation identifier, or <c>null</c> for a legacy archive.</param>
public record ArchivedCommand(
    string Tenant,
    string Domain,
    string AggregateId,
    string CommandType,
    byte[] Payload,
    Dictionary<string, string>? Extensions,
    DateTimeOffset OriginalTimestamp,
    string? MessageId = null,
    string? CorrelationId = null);
