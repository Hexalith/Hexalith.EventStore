namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>Persisted selection metadata carried by an existing model manifest and terminal batch receipt.</summary>
/// <param name="Version">The selection version.</param>
/// <param name="TenantId">The exact tenant.</param>
/// <param name="Domain">The exact domain.</param>
/// <param name="AggregateId">The source aggregate.</param>
/// <param name="ProjectionName">The named projection.</param>
/// <param name="OwnerIdentity">The selected owner identity.</param>
/// <param name="OwnerGeneration">The immutable selected generation.</param>
/// <param name="AppliedSequence">The completely applied source sequence.</param>
/// <param name="MessageId">The persisted event identity at the applied head.</param>
/// <param name="BatchId">The existing immutable batch identity.</param>
/// <param name="FreshnessVersion">The persisted read-model freshness version.</param>
public sealed record ProjectionOwnerSelection(int Version, string TenantId, string Domain, string AggregateId,
    string ProjectionName, string OwnerIdentity, string OwnerGeneration, long AppliedSequence,
    string MessageId, string BatchId, string FreshnessVersion)
{
    /// <summary>Gets the existing coordinated receipt's projection scope when it differs from this route.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ReceiptProjectionName { get; init; }
}
