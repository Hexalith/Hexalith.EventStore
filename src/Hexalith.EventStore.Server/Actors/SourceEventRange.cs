using System.Runtime.Serialization;

using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Returns an actor-measured compact event array without repeating envelope sizing at the caller.</summary>
/// <param name="Events">The ordered envelopes returned within both requested limits.</param>
/// <param name="EnvelopeArrayBytes">The exact compact UTF-8 event-array length, including framing.</param>
[DataContract]
public sealed record SourceEventRange(
    [property: DataMember] EventEnvelope[] Events,
    [property: DataMember] long EnvelopeArrayBytes)
{
    /// <summary>A closed expected-failure code survives actor remoting without exception-text interpretation.</summary>
    [DataMember(EmitDefaultValue = false)]
    public string? ReasonCode { get; init; }
}
