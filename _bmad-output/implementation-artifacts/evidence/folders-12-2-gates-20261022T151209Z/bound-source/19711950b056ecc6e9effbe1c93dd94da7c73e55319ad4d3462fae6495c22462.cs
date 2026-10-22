using System.Runtime.Serialization;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Ephemeral actor request carrying a command and separately signed human authority.</summary>
/// <param name="Command">The command envelope.</param>
/// <param name="HumanAuthorityReceipt">The command-bound human receipt.</param>
[DataContract]
public sealed record AuthorizedCommandEnvelope(
    [property: DataMember] CommandEnvelope Command,
    [property: DataMember] string HumanAuthorityReceipt);
