using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Observes every serialized metadata and payload getter before output production.</summary>
internal sealed class DaprLogicalCommandStateSerializedEvent : ISerializedEventPayload
{
    /// <summary>Gets or sets each individual application getter hook.</summary>
    internal Action<string>? Hook
    {
        get; set;
    }
    /// <inheritdoc/>
    public string EventTypeName
    {
        get
        {
            Hook?.Invoke("alias");
            return "result";
        }
    }
    /// <inheritdoc/>
    public string SerializationFormat
    {
        get
        {
            Hook?.Invoke("format");
            return "json";
        }
    }
    /// <inheritdoc/>
    public int? MetadataVersion
    {
        get
        {
            Hook?.Invoke("metadata");
            return 1;
        }
    }
    /// <inheritdoc/>
    public string? EventContractType
    {
        get
        {
            Hook?.Invoke("contract");
            return null;
        }
    }
    /// <inheritdoc/>
    public int? PayloadVersion
    {
        get
        {
            Hook?.Invoke("version");
            return null;
        }
    }
    /// <inheritdoc/>
    public byte[] PayloadBytes
    {
        get
        {
            Hook?.Invoke("bytes");
            return "{}"u8.ToArray();
        }
    }
}
