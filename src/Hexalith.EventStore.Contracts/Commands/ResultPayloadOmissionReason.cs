using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>Explains an absent result payload when the gateway can establish the command outcome.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ResultPayloadOmissionReason>))]
public enum ResultPayloadOmissionReason
{
    /// <summary>The existing command is still being processed or recovered.</summary>
    InFlight = 0,

    /// <summary>Event publication failed and automatic recovery is explicitly unavailable.</summary>
    PublishFailed = 1,
}
