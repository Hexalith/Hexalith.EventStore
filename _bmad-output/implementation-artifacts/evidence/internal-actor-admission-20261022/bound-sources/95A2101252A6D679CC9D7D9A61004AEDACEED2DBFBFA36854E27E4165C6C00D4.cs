using System.Runtime.Serialization;

using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Returns actor-owned stream metadata without retrieving event envelopes.</summary>
/// <param name="Status">Whether persisted source evidence is supported and valid.</param>
/// <param name="Exists">Whether aggregate metadata is present.</param>
/// <param name="CurrentSequence">The authoritative aggregate-local head.</param>
/// <param name="Erased">Whether actor-owned erasure has durably started or completed; an in-progress erasure is Invalid.</param>
/// <param name="Proof">The validated proof, when available.</param>
[DataContract]
public sealed record SourceProofInspection(
    [property: DataMember] SourceProofInspectionStatus Status,
    [property: DataMember] bool Exists,
    [property: DataMember] long CurrentSequence,
    [property: DataMember] bool Erased,
    [property: DataMember] SourceProof? Proof);
