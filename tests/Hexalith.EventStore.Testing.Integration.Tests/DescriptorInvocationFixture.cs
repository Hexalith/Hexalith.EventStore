using Hexalith.EventStore.Testing.Integration;

namespace Hexalith.EventStore.Testing.Integration.Tests;

/// <summary>Requests the explicit credential-bound endpoint without deriving its callee or private key.</summary>
internal sealed class DescriptorInvocationFixture : AspireTopologyFixtureBase
{
    protected override IReadOnlyList<TestEnvironmentEndpoint> Resources =>
        [new("eventstore", "Hexalith.EventStore", "dapr-invocation", TimeSpan.FromSeconds(2)) { DaprInvocation = true }];

    internal HttpClient InvocationClient => Client("eventstore", "Hexalith.EventStore", "dapr-invocation");
}
