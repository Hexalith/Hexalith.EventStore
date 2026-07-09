namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>
/// xUnit collection definition for sharing the Dapr test fixture across integration tests.
/// All tests decorated with [Collection("DaprTestContainer")] share the same Dapr sidecar process.
/// The fixture mutates process-wide DAPR port environment variables and exposes mutable fake
/// services to the test host, so it cannot safely run in parallel with other collections.
/// </summary>
[CollectionDefinition("DaprTestContainer", DisableParallelization = true)]
public sealed class DaprTestContainerCollection : ICollectionFixture<DaprTestContainerFixture>;
