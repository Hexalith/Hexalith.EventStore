namespace Hexalith.EventStore.Platform.IntegrationTests;

using Hexalith.EventStore.Testing.Integration;

/// <summary>Consumes the runner-owned gateway and isolated persistence endpoints without starting services.</summary>
public sealed class PlatformEnvironmentFixture : AspireTopologyFixtureBase
{
    /// <inheritdoc />
    protected override IReadOnlyList<TestEnvironmentEndpoint> Resources =>
    [
        new("eventstore", "eventstore", "http", TimeSpan.FromSeconds(30)),
        new("redis", "eventstore", "tcp", TimeSpan.FromSeconds(30), Authenticated: false),
    ];

    /// <summary>Gets the bounded authenticated gateway client supplied by the descriptor.</summary>
    public HttpClient Gateway => Client("eventstore");
}
