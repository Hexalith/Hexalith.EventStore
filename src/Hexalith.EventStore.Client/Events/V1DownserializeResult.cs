namespace Hexalith.EventStore.Client.Events;

/// <summary>Describes the V1 alias and source version emitted by a downserialization edge.</summary>
/// <param name="Domain">The owning domain.</param>
/// <param name="EventContractType">The canonical event contract type.</param>
/// <param name="EventTypeName">The exact registered legacy alias.</param>
/// <param name="SourcePayloadVersion">The alias's declared source payload version.</param>
/// <param name="SerializationFormat">The output serialization format.</param>
[Obsolete("Legacy downserialization compatibility contract; no new downserialization API is provided.")]
public sealed record V1DownserializeResult(
    string Domain,
    string EventContractType,
    string EventTypeName,
    int SourcePayloadVersion,
    string SerializationFormat);
