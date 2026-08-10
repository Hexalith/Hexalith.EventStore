using Shouldly;

namespace Hexalith.EventStore.ProviderVerification.Tests;

public sealed class ProviderStateCoordinatorTests
{
    [Fact]
    public async Task ApplyAsync_KnownState_RecordsSetupAndTeardown()
    {
        var coordinator = new ProviderStateCoordinator(new HashSet<string>(["known"], StringComparer.Ordinal));
        coordinator.BeginInteraction("known");

        (await coordinator.ApplyAsync("known", "setup", TestContext.Current.CancellationToken)).ShouldBe("state.setup.succeeded");
        (await coordinator.ApplyAsync("known", "teardown", TestContext.Current.CancellationToken)).ShouldBe("state.teardown.succeeded");

        coordinator.CurrentState.ShouldBeNull();
        coordinator.SnapshotEvents().Select(item => item.Action).ShouldBe(["setup", "teardown"]);
    }

    [Fact]
    public async Task ApplyAsync_MissingState_RejectsWithoutMutation()
    {
        var coordinator = new ProviderStateCoordinator(new HashSet<string>(["known"], StringComparer.Ordinal));
        coordinator.BeginInteraction("known");

        string result = await coordinator.ApplyAsync("missing", "setup", TestContext.Current.CancellationToken);

        result.ShouldBe("state.unknown-or-unexpected");
        coordinator.CurrentState.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_TransitionExceedsBudget_ObservesCancellation()
    {
        var coordinator = new ProviderStateCoordinator(
            new HashSet<string>(["known"], StringComparer.Ordinal),
            TimeSpan.FromSeconds(2));
        coordinator.BeginInteraction("known");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));

        await Should.ThrowAsync<OperationCanceledException>(
            () => coordinator.ApplyAsync("known", "setup", cancellation.Token));

        coordinator.CurrentState.ShouldBeNull();
    }

    [Fact]
    public async Task ForceCleanup_InjectedFailure_RemainsExplicitlyDirty()
    {
        var coordinator = new ProviderStateCoordinator(
            new HashSet<string>(["known"], StringComparer.Ordinal),
            failForcedCleanup: true);
        coordinator.BeginInteraction("known");
        _ = await coordinator.ApplyAsync("known", "setup", TestContext.Current.CancellationToken);

        coordinator.ForceCleanup("known").ShouldBeFalse();

        coordinator.CurrentState.ShouldBe("known");
        coordinator.SnapshotEvents().Last().ResultCode.ShouldBe("state.teardown.failed");
    }
}
