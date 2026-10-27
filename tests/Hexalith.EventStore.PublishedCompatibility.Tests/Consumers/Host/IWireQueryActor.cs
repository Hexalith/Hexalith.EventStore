#if SELECTED
using Dapr.Actors;
using Hexalith.EventStore.Contracts.Queries;

namespace PublishedConsumer;

/// <summary>Mirrors the supported projection query contract for real typed Dapr remoting.</summary>
public interface IWireQueryActor : IActor, IProjectionActor;
#endif
