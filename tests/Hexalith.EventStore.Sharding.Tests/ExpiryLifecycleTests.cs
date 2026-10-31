using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>An explicit short-deadline operator run proves actual expiry during startup.</summary>
[Collection("ShardingLive")]
public sealed class ExpiryLifecycleTests
{
    /// <summary>The real watchdog fences startup and verifies owned-resource teardown.</summary>
    [Fact(Explicit = true)]
    public async Task OperatorExpiryCancelsAndTearsDownOwnedResourcesAsync()
    {
        await using var fixture = new ShardingFixture();
        try
        {
            await fixture.InitializeAsync();
        }
        catch (OperationCanceledException)
        {
            // Expiry may cancel startup before both actor runtimes become ready.
            await fixture.AwaitExpiryAsync();
            fixture.CleanupVerified.ShouldBeTrue();
            return;
        }

        await fixture.AwaitExpiryAsync();
        fixture.CleanupVerified.ShouldBeTrue();
        Console.WriteLine("Retained expiry artifacts: " + fixture.Artifacts);
    }
}
