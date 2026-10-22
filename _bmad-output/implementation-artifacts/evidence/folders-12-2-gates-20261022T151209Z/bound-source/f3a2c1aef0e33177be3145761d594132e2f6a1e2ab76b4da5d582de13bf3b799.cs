using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Reads stable directory authority for internal migration reproof without mutating it.</summary>
internal interface IIdempotencyAdmissionDirectoryInspectionActor : IActor
{
    /// <summary>Reads existing canonical authority without creating or advancing directory state.</summary>
    Task<IdempotencyAdmissionDirectoryResult?> InspectAsync(IdempotencyAdmissionDirectoryAlias[] aliases);
}
