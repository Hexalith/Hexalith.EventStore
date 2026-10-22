namespace Hexalith.EventStore.IntegrationTests.Fixtures;

/// <summary>Isolates the PubSub projection profile from ordinary Aspire contract tests.</summary>
[CollectionDefinition("AspirePubSubProjectionTests", DisableParallelization = true)]
public sealed class AspirePubSubProjectionTestCollection : ICollectionFixture<AspirePubSubProjectionTestFixture>;
