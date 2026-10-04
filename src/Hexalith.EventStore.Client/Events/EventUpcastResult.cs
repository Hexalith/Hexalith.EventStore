namespace Hexalith.EventStore.Client.Events;

/// <summary>Describes the canonical identity and format produced by one upcast edge.</summary>
/// <param name="Domain">The owning domain.</param>
/// <param name="EventContractType">The canonical event contract type.</param>
/// <param name="PayloadVersion">The output payload version.</param>
/// <param name="SerializationFormat">The output serialization format.</param>
public sealed record EventUpcastResult(
    string Domain,
    string EventContractType,
    int PayloadVersion,
    string SerializationFormat);
