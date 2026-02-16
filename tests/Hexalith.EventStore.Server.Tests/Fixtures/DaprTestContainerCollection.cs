namespace Hexalith.EventStore.Server.Tests.Fixtures;

/// <summary>
/// xUnit collection definition for sharing the Dapr test container fixture across integration tests.
/// All tests decorated with [Collection("DaprTestContainer")] share the same Redis + Dapr sidecar instance.
/// </summary>
[CollectionDefinition("DaprTestContainer")]
public sealed class DaprTestContainerCollection : ICollectionFixture<DaprTestContainerFixture>;
