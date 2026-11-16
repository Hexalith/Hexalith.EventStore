using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.Security;

/// <summary>Exercises exact container identity, disappearance and retry at the Docker boundary.</summary>
public sealed class OwnedAuthenticationContainerTests
{
    /// <summary>Removes only the captured ID even when its name can be reused.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposalNeverTargetsAReplacementName(bool disappearsDuringRemoval)
    {
        string identity = new('a', 64);
        var calls = new List<string[]>();
        OwnedAuthenticationContainer? container = null;
        container = new OwnedAuthenticationContainer((arguments, _) =>
        {
            calls.Add(arguments);
            if (arguments[0] == "run") return Task.FromResult(identity);
            if (arguments[0] == "inspect")
                return Task.FromResult(JsonSerializer.Serialize(new { id = identity, owner = container!.Owner }));
            arguments.ShouldBe(["rm", "--force", identity]);
            if (disappearsDuringRemoval) throw new InvalidOperationException("No such container");
            return Task.FromResult(identity);
        });
        await container.StartAsync("unused", null, 6379, [], TestContext.Current.CancellationToken);
        await container.DisposeAsync();
        await container.DisposeAsync();
        calls.Single(arguments => arguments[0] == "inspect")[^1].ShouldBe(identity);
        calls.Count(arguments => arguments[0] == "rm").ShouldBe(1);
    }

    /// <summary>Treats a vanished ID as released without looking up its reusable name.</summary>
    [Fact]
    public async Task DisappearedIdentityDoesNotInspectOrRemoveItsReplacementName()
    {
        string identity = new('b', 64);
        var calls = new List<string[]>();
        var container = new OwnedAuthenticationContainer((arguments, _) =>
        {
            calls.Add(arguments);
            if (arguments[0] == "run") return Task.FromResult(identity);
            arguments.ShouldBe(["inspect", "--format", arguments[2], identity]);
            throw new InvalidOperationException("No such object");
        });
        await container.StartAsync("unused", null, 6379, [], TestContext.Current.CancellationToken);
        await container.DisposeAsync();
        calls.Count.ShouldBe(2);
    }

    /// <summary>Refuses foreign ownership and retains failed exact-ID cleanup for retry.</summary>
    [Fact]
    public async Task FailedRemovalRetainsIdentityForRetryAndForeignOwnerIsRefused()
    {
        string identity = new('c', 64);
        int removals = 0;
        bool foreign = true;
        OwnedAuthenticationContainer? container = null;
        container = new OwnedAuthenticationContainer((arguments, _) =>
        {
            if (arguments[0] == "run") return Task.FromResult(identity);
            if (arguments[0] == "inspect")
            {
                arguments[^1].ShouldBe(identity);
                return Task.FromResult(JsonSerializer.Serialize(new { id = identity, owner = container!.Owner + (foreign ? " " : "") }));
            }
            arguments.ShouldBe(["rm", "--force", identity]);
            if (++removals == 1) throw new InvalidOperationException("Daemon unavailable");
            return Task.FromResult(identity);
        });
        await container.StartAsync("unused", null, 6379, [], TestContext.Current.CancellationToken);
        await Should.ThrowAsync<InvalidOperationException>(() => container.DisposeAsync().AsTask());
        removals.ShouldBe(0);
        foreign = false;
        await Should.ThrowAsync<InvalidOperationException>(() => container.DisposeAsync().AsTask());
        await container.DisposeAsync();
        removals.ShouldBe(2);
    }
}
