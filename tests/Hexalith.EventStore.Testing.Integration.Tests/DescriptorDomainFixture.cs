using Hexalith.EventStore.Testing.Integration;

namespace Hexalith.EventStore.Testing.Integration.Tests;

internal sealed class DescriptorDomainFixture : DaprDomainServiceTestFixtureBase
{
    protected override TestEnvironmentEndpoint DomainService => new("eventstore", "Hexalith.EventStore", "http", TimeSpan.FromSeconds(2));

    protected override TestEnvironmentEndpoint Sidecar => new("eventstore", "Hexalith.EventStore", "dapr-http", TimeSpan.FromSeconds(2), false);
    internal HttpClient SidecarClient => Client(Sidecar.ResourceId, Sidecar.ModuleId, Sidecar.EndpointName);

    internal HttpClient AmbiguousClient => Client("eventstore");

    internal HttpClient ExactClient(string resource, string module, string endpoint) => Client(resource, module, endpoint);
}
