using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Replay;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class AsyncAggregateReplayRoutingTests
{
    [Fact]
    public async Task ReplayAsync_SelectsExplicitAggregateRouteWithoutCommandProcessor()
    {
        using var cancellation = new CancellationTokenSource();
        IAsyncAggregateReplay replay = Substitute.For<IAsyncAggregateReplay>();
        replay.CanReplayAggregateType("widget").Returns(true);
        CancellationToken observed = default;
        replay.ReplayAsync(Arg.Any<AggregateReconstructionRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                observed = call.ArgAt<CancellationToken>(1);
                return Task.FromResult(AggregateReconstructionResult.Succeeded("{}", 0));
            });
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAsyncAggregateReplay>("widget", replay);
        using ServiceProvider provider = services.BuildServiceProvider();

        AggregateReconstructionResult result = await DomainServiceRequestRouter.ReplayAsync(provider, Request(), cancellation.Token);

        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        observed.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task ReplayAsync_RejectsForeignAggregateBeforeInvocation()
    {
        IAsyncAggregateReplay replay = Substitute.For<IAsyncAggregateReplay>();
        replay.CanReplayAggregateType("widget").Returns(false);
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAsyncAggregateReplay>("widget", replay);
        using ServiceProvider provider = services.BuildServiceProvider();

        AggregateReconstructionResult result = await DomainServiceRequestRouter.ReplayAsync(provider, Request());

        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnknownAggregateType);
        _ = replay.DidNotReceiveWithAnyArgs().ReplayAsync(default!, default);
    }

    [Fact]
    public async Task ReplayAsync_RejectsAmbiguousAggregateOwnershipBeforeInvocation()
    {
        IAsyncAggregateReplay first = Substitute.For<IAsyncAggregateReplay>();
        IAsyncAggregateReplay second = Substitute.For<IAsyncAggregateReplay>();
        first.CanReplayAggregateType("widget").Returns(true);
        second.CanReplayAggregateType("widget").Returns(true);
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAsyncAggregateReplay>("widget", first);
        services.AddKeyedSingleton<IAsyncAggregateReplay>("widget", second);
        using ServiceProvider provider = services.BuildServiceProvider();

        AggregateReconstructionResult result = await DomainServiceRequestRouter.ReplayAsync(provider, Request());

        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnknownAggregateType);
        _ = first.DidNotReceiveWithAnyArgs().ReplayAsync(default!, default);
        _ = second.DidNotReceiveWithAnyArgs().ReplayAsync(default!, default);
    }

    [Fact]
    public async Task ReplayAsync_RejectsPagedContextWithoutAsyncOwner()
    {
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        AggregateReconstructionRequest request = Request() with { PagedContext = EmptyPagedContext() };

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => DomainServiceRequestRouter.ReplayAsync(provider, request));
    }

    [Fact]
    public async Task ReplayAsync_RejectsPagedContextBeforeAsyncHandlerInvocation()
    {
        IAsyncAggregateReplay replay = Substitute.For<IAsyncAggregateReplay>();
        replay.CanReplayAggregateType("widget").Returns(true);
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAsyncAggregateReplay>("widget", replay);
        using ServiceProvider provider = services.BuildServiceProvider();

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => DomainServiceRequestRouter.ReplayAsync(
                provider, Request() with { PagedContext = EmptyPagedContext() }));
        _ = replay.DidNotReceiveWithAnyArgs().ReplayAsync(default!, default);
    }

    [Fact]
    public async Task ReplayAsync_RejectsIncompleteResultOnWholeArrayRoute()
    {
        IAsyncAggregateReplay replay = Substitute.For<IAsyncAggregateReplay>();
        replay.CanReplayAggregateType("widget").Returns(true);
        replay.ReplayAsync(Arg.Any<AggregateReconstructionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AggregateReconstructionResult(
                AggregateReconstructionStatus.InProgress, null, 0, null, null,
                AggregateReconstructionErrorCategory.None, null, null)));
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAsyncAggregateReplay>("widget", replay);
        using ServiceProvider provider = services.BuildServiceProvider();

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => DomainServiceRequestRouter.ReplayAsync(provider, Request()));
    }

    private static PagedContext EmptyPagedContext()
        => new([], [1], "tenant", "widget", "widget", "aggregate", 0, 0, 1, 0, 0,
            "operation", [2], null, new byte[32], new byte[32], true,
            Substitute.For<IPagedReplayStateSession>());

    private static AggregateReconstructionRequest Request()
        => new("tenant", "widget", "widget", "aggregate", 0, [], false, null);
}
