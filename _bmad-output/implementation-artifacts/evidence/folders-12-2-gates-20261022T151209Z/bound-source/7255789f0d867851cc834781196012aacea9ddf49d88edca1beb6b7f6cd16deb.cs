using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Requests one canonical admission authority for active and retained protected aliases.</summary>
/// <param name="SchemaVersion">The directory schema version.</param>
/// <param name="ActiveActorId">The sole active-writer actor alias.</param>
/// <param name="Aliases">The active alias followed by retained-reader aliases.</param>
/// <param name="ExistingActorId">An inspected existing authority, if any.</param>
[DataContract]
public sealed record IdempotencyAdmissionDirectoryRequest(
    [property: DataMember] int SchemaVersion,
    [property: DataMember] string ActiveActorId,
    [property: DataMember] IdempotencyAdmissionDirectoryAlias[] Aliases,
    [property: DataMember] string? ExistingActorId = null);
