using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Retains each native proof inspection while reducing actor remoting for one stream request.</summary>
/// <param name="Before">The first fresh actor-owned source observation.</param>
/// <param name="After">The second observation for a metadata-only request.</param>
/// <param name="Range">The actor-validated bounded range for a page request.</param>
[DataContract]
public sealed record SourceNativeReadBatch(
    [property: DataMember] SourceProofInspection Before,
    [property: DataMember] SourceProofInspection? After,
    [property: DataMember] SourceEventRange? Range);
