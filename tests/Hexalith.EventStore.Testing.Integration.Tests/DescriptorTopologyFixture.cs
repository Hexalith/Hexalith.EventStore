using Hexalith.EventStore.Testing.Integration;

namespace Hexalith.EventStore.Testing.Integration.Tests;

internal sealed class DescriptorTopologyFixture : AspireTopologyFixtureBase
{
    protected override IReadOnlyList<TestEnvironmentEndpoint> Resources => [new("eventstore", "Hexalith.EventStore", "http", TimeSpan.FromSeconds(2))];

    internal Uri? Endpoint => Client("eventstore").BaseAddress;
}
