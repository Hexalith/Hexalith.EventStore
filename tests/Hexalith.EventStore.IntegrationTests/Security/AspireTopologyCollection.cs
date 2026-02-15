namespace Hexalith.EventStore.IntegrationTests.Security;

/// <summary>
/// xUnit collection definition that shares a single <see cref="AspireTopologyFixture"/>
/// across all E2E security test classes. This starts the Aspire topology (CommandApi,
/// sample domain service, Keycloak, Redis, DAPR sidecars) ONCE instead of per-class,
/// reducing E2E test execution time from ~3x startup to ~1x.
/// </summary>
[CollectionDefinition("AspireTopology")]
public class AspireTopologyCollection : ICollectionFixture<AspireTopologyFixture>
{
}
