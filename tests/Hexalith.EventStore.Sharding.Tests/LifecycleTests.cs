using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Live failure-path teardown, using only exact fixture-owned identities.</summary>
public sealed class LifecycleTests
{
    /// <summary>Partial creation still removes and verifies every owned resource.</summary>
    /// <param name="after">The deterministic partial-startup point.</param>
    [Theory]
    [InlineData("postgres")]
    [InlineData("placement")]
    [InlineData("node")]
    [InlineData("missing-cid")]
    public async Task PartialStartupFailureVerifiesOwnedCleanupAsync(string after)
    {
        await using var fixture = new ShardingFixture();
        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() => fixture.InitializeAsync(after));
        error.Message.ShouldContain("Injected partial-startup failure after " + after);
        fixture.CleanupVerified.ShouldBeTrue();
        if (after == "missing-cid") fixture.RecoveredContainerCount.ShouldBe(1);
        Console.WriteLine("Retained failure-path artifacts: " + fixture.Artifacts);
    }
}
