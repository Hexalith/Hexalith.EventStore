using Hexalith.EventStore.Testing.Integration;

namespace Hexalith.EventStore.Testing.Integration.Tests;

/// <summary>Requires both a client and the exact non-HTTP persistence binding before exposing either.</summary>
internal sealed class DescriptorPersistenceFixture : AspireTopologyFixtureBase
{
    /// <inheritdoc/>
    protected override IReadOnlyList<TestEnvironmentEndpoint> Resources =>
    [
        new("eventstore", "Hexalith.EventStore", "http", TimeSpan.FromSeconds(1)),
        new("redis", "parties", "tcp", TimeSpan.FromSeconds(1), Authenticated: false),
    ];

    /// <summary>Gets the HTTP client only after every required binding has passed initialization.</summary>
    internal HttpClient Gateway => Client("eventstore");

    /// <summary>Attempts a client lookup for the persistence binding, which must never allocate an HTTP client.</summary>
    internal HttpClient PersistenceClient => Client("redis");
}
