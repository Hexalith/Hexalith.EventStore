namespace Hexalith.EventStore.IntegrationTests.Fixtures;

/// <summary>Starts the shared Aspire contract topology with test-only projection PubSub transport.</summary>
public sealed class AspirePubSubProjectionTestFixture : AspireContractTestFixture
{
    /// <inheritdoc/>
    protected override bool UsePubSubProjectionTransport => true;
}
