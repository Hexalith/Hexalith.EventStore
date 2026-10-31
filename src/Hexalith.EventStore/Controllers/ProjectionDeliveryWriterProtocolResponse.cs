using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Controllers;

/// <summary>The existing writer protocol activation success wire shape.</summary>
/// <param name="Status">Existing status value.</param>
public sealed record ProjectionDeliveryWriterProtocolResponse(
    [property: JsonPropertyName("status")] string Status);
