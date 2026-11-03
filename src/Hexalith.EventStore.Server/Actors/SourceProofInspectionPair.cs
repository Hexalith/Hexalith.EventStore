using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Returns two actual metadata-only source inspections performed within one authoritative actor turn.</summary>
/// <param name="Initial">The existing initial source inspection.</param>
/// <param name="Final">The existing final source inspection; consumers must compare the complete observations.</param>
[DataContract]
public sealed record SourceProofInspectionPair(
    [property: DataMember] SourceProofInspection Initial,
    [property: DataMember] SourceProofInspection Final);
