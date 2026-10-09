using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;
/// <summary>Marks the dormant dedicated actor without exposing an invocation or mutation method.</summary>
internal interface IDaprLogicalCheckpointActor : IActor
{
}
