using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Immutable source, route and selected-generation admission of one stable delivery reservation.</summary>
internal sealed record ProjectionDeliveryGuardReservation(SourceProof BaseSource, SourceProof AdmittedSource,
    string CatalogFingerprint, string HandlerSemantics, string SelectedOwnerGeneration,
    ProjectionOwnerProof? BaseOwnerProof)
{
    /// <summary>Gets whether this same reservation owns an immutable canonical staged replacement.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool CanonicalReplay { get; init; }
}
