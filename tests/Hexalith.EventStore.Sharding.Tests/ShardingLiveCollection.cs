namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Serializes live fixtures so temporary port selection cannot race another lane fixture.</summary>
[CollectionDefinition("ShardingLive", DisableParallelization = true)]
public sealed class ShardingLiveCollection;
