using Dapr.Actors;

namespace Hexalith.EventStore.Sharding.TestHost;

/// <summary>The isolated synthetic actor protocol; never registered in production.</summary>
public interface IShardActor : IActor
{
    /// <summary>Executes a serialized operation on one exact pair.</summary>
    /// <param name="request">The synthetic operation.</param>
    /// <returns>The complete range identity or denial.</returns>
    Task<ShardReply> ApplyAsync(ShardRequest request);
}
