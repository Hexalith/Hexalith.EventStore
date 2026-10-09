namespace Hexalith.EventStore.Client.Events;

/// <summary>The in-memory view of a stored payload after any required upcast.</summary>
/// <param name="EventTypeName">The effective event name.</param>
/// <param name="EventType">The registered CLR type, or null for an unknown event.</param>
/// <param name="Payload">The effective JSON bytes; unchanged when no step ran.</param>
/// <param name="PayloadVersion">The effective payload version.</param>
public sealed record ResolvedEventPayload(string EventTypeName, Type? EventType, byte[] Payload, int PayloadVersion);
