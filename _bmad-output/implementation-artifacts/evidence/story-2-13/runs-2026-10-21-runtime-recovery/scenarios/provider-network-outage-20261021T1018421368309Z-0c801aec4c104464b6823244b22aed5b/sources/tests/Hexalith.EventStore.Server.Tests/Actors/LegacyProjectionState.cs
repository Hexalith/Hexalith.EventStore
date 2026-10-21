using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Retains the pre-detail actor wire contract for meaningful old/new serialization vectors.</summary>
[DataContract(Name = "ProjectionState", Namespace = "http://schemas.datacontract.org/2004/07/Hexalith.EventStore.Server.Actors")]
public sealed record LegacyProjectionState(
    [property: DataMember] string ProjectionType,
    [property: DataMember] string TenantId,
    [property: DataMember] byte[] StateBytes);
