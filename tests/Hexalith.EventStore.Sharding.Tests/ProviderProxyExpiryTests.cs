using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>An explicit operator deadline proves live proxy listener/session teardown.</summary>
[Collection("ShardingLive")]
public sealed class ProviderProxyExpiryTests
{
    /// <summary>Expiry drains the actual proxy after startup; choose sufficient time for readiness.</summary>
    [Fact(Explicit = true)]
    public async Task OperatorExpiryDrainsOwnedProviderProxyAsync()
    {
        await using var fixture = new ShardingFixture();
        try { await fixture.InitializeAsync(providerCommitProxy: true); }
        catch (OperationCanceledException)
        {
            await fixture.AwaitExpiryAsync();
            fixture.CleanupVerified.ShouldBeTrue();
            fixture.ProxyStopped.ShouldBeTrue(); // A deadline before listener creation does not prove this case.
            return;
        }

        await fixture.AwaitExpiryAsync();
        fixture.CleanupVerified.ShouldBeTrue();
        fixture.ProxyStopped.ShouldBeTrue();
        Console.WriteLine("Retained proxy expiry artifacts: " + fixture.Artifacts);
    }
}
