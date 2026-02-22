namespace Hexalith.EventStore.Server.Tests.Fixtures;

/// <summary>
/// xUnit collection definition for sharing the Dapr test fixture across integration tests.
/// All tests decorated with [Collection("DaprTestContainer")] share the same Dapr sidecar process.
/// </summary>
[CollectionDefinition("DaprTestContainer")]
public sealed class DaprTestContainerCollection : ICollectionFixture<DaprTestContainerFixture>;
