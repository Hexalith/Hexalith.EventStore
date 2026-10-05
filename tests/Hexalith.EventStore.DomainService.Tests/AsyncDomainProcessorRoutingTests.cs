using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class AsyncDomainProcessorRoutingTests
{
    [Fact]
    public async Task ProcessAsync_PrefersAsyncProcessorAndForwardsOriginalToken()
    {
        using var cancellation = new CancellationTokenSource();
        IAsyncDomainProcessor asyncProcessor = Substitute.For<IAsyncDomainProcessor>();
        IDomainProcessor legacyProcessor = Substitute.For<IDomainProcessor>();
        CancellationToken observed = default;
        asyncProcessor.ProcessAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                observed = call.ArgAt<CancellationToken>(2);
                return Task.FromResult(DomainResult.NoOp());
            });
        using ServiceProvider provider = BuildProvider(asyncProcessor, legacyProcessor);

        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider, Request(), cancellation.Token);

        result.Events.ShouldBeEmpty();
        observed.ShouldBe(cancellation.Token);
        _ = legacyProcessor.DidNotReceiveWithAnyArgs().ProcessAsync(default!, default);
    }

    [Fact]
    public async Task ProcessAsync_CancellationAfterLegacyProcessorDiscardsItsResult()
    {
        using var cancellation = new CancellationTokenSource();
        IDomainProcessor legacyProcessor = Substitute.For<IDomainProcessor>();
        legacyProcessor.ProcessAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromResult(DomainResult.NoOp());
            });
        using ServiceProvider provider = BuildProvider(null, legacyProcessor);

        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(
            () => DomainServiceRequestRouter.ProcessAsync(provider, Request(), cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        _ = legacyProcessor.ReceivedWithAnyArgs(1).ProcessAsync(default!, default);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProcessAsync_RefusesUnprovenVersionedIntakeBeforeProcessor(bool modeBearing)
    {
        IAsyncDomainProcessor asyncProcessor = Substitute.For<IAsyncDomainProcessor>();
        IDomainProcessor legacyProcessor = Substitute.For<IDomainProcessor>();
        using ServiceProvider provider = BuildProvider(asyncProcessor, legacyProcessor);
        DomainServiceRequest request = modeBearing
            ? Request() with { WriterMode = "V2", RegistryFingerprint = new string('a', 64) }
            : Request() with { CommandStateProof = [1] };

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => DomainServiceRequestRouter.ProcessAsync(provider, request));

        exception.Message.ShouldContain("CapabilityMismatch");
        _ = asyncProcessor.DidNotReceiveWithAnyArgs().ProcessAsync(default!, default, default);
        _ = legacyProcessor.DidNotReceiveWithAnyArgs().ProcessAsync(default!, default);
    }

    private static ServiceProvider BuildProvider(IAsyncDomainProcessor? asyncProcessor, IDomainProcessor legacyProcessor)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IDomainProcessor>("widget", legacyProcessor);
        if (asyncProcessor is not null)
        {
            services.AddKeyedSingleton<IAsyncDomainProcessor>("widget", asyncProcessor);
        }

        return services.BuildServiceProvider();
    }

    private static DomainServiceRequest Request()
        => new(new CommandEnvelope("message", "tenant", "widget", "aggregate", "Create", [], "correlation", null, "user", null), null);
}
