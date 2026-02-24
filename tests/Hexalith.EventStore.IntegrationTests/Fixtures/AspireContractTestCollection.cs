namespace Hexalith.EventStore.IntegrationTests.Fixtures;

/// <summary>
/// xUnit collection definition that shares a single <see cref="AspireContractTestFixture"/>
/// across all Tier 3 contract test classes. Starts the Aspire topology (CommandApi,
/// sample domain service, Redis, DAPR sidecars) ONCE without Keycloak for fast execution.
/// </summary>
[CollectionDefinition("AspireContractTests")]
public class AspireContractTestCollection : ICollectionFixture<AspireContractTestFixture> {
}
