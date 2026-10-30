using Hexalith.EventStore.Testing.Integration;

namespace Hexalith.EventStore.Testing.Integration.Tests;

internal sealed class DescriptorDomainFixture : DaprDomainServiceTestFixtureBase
{
    protected override TestEnvironmentEndpoint DomainService => new("eventstore", "Hexalith.EventStore", "http", TimeSpan.FromSeconds(2));

    protected override TestEnvironmentEndpoint Sidecar => new("sidecar", "Hexalith.EventStore", "http", TimeSpan.FromSeconds(2), false);
}
