using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Controllers;

/// <summary>The existing delivery reconciliation success wire shape.</summary>
/// <param name="Status">Existing status value.</param>
/// <param name="ReasonCode">Existing reasonCode value.</param>
/// <param name="PreservedSequence">Existing preservedSequence value.</param>
public sealed record ProjectionDeliveryReconciliationResponse(
    [property: JsonPropertyName("status")] string Status,
    string ReasonCode,
    long PreservedSequence);
