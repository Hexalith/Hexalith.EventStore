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
public record ArchivedCommand(
    string Tenant,
    string Domain,
    string AggregateId,
    string CommandType,
    byte[] Payload,
    Dictionary<string, string>? Extensions,
    DateTimeOffset OriginalTimestamp);
