using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Checks private whole-array admission around every legacy replay route.</summary>
public sealed class LegacyAggregateReplayRoutingTests
{
    [Theory]
    [InlineData(false, 100_001)]
    [InlineData(true, 100_001)]
    [InlineData(false, 32_769)]
    [InlineData(true, 32_769)]
    public async Task OversizedSourceRefusesBeforeReadsOrServiceResolution(bool asynchronous, int count)
    {
        var source = new ReplayAdmissionProbeCollection(count);
        IServiceProvider provider = Substitute.For<IServiceProvider>();

        AggregateReconstructionResult result = asynchronous
            ? await DomainServiceRequestRouter.ReplayAsync(provider, Request(source))
            : DomainServiceRequestRouter.Replay(provider, Request(source));

        result.ReasonCode.ShouldBe("LegacyArrayLimit");
        result.StateJson.ShouldBeNull();
        result.Timeline.ShouldBeNull();
        source.CountReads.ShouldBe(1);
        source.IndexReads.ShouldBe(0);
        provider.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task SourceCountCancellationPrecedesTypedLimitRefusal()
    {
        using var cancellation = new CancellationTokenSource();
        var source = new ReplayAdmissionProbeCollection(100_001, cancellation.Cancel);
        IServiceProvider provider = Substitute.For<IServiceProvider>();

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(
            () => DomainServiceRequestRouter.ReplayAsync(provider, Request(source), cancellation.Token));

        error.CancellationToken.ShouldBe(cancellation.Token);
        source.CountReads.ShouldBe(1);
        source.IndexReads.ShouldBe(0);
        provider.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterMetadataRefusalPrecedesOwnershipCallbacks(bool asynchronous)
    {
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        ReplayEventEnvelope[] source = [Event(1), Event(2) with { EventTypeName = new string('x', 600_000) }];
        byte[] original = source[0].Payload.ToArray();

        AggregateReconstructionResult result = asynchronous
            ? await DomainServiceRequestRouter.ReplayAsync(provider, Request(source))
            : DomainServiceRequestRouter.Replay(provider, Request(source));

        result.ReasonCode.ShouldBe("MetadataLimit");
        source[0].Payload.ShouldBe(original);
        provider.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, 2, 3, 1)]
    [InlineData(true, 2, 3, 1)]
    [InlineData(false, 1, 3, 2)]
    [InlineData(true, 1, 3, 2)]
    [InlineData(false, 1, 1, 1)]
    [InlineData(true, 1, 1, 1)]
    public async Task IncompleteOrDuplicatePrefixRefusesBeforeOwnershipCallbacks(
        bool asynchronous, long first, long second, long failedSequence)
    {
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        var request = Request([Event(first), Event(second)]) with { UpToSequence = 3 };

        AggregateReconstructionResult result = asynchronous
            ? await DomainServiceRequestRouter.ReplayAsync(provider, request)
            : DomainServiceRequestRouter.Replay(provider, request);

        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.Unexpected);
        result.FailedSequenceNumber.ShouldBe(failedSequence);
        result.StateJson.ShouldBeNull();
        result.Timeline.ShouldBeNull();
        provider.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task OwnershipMutationCannotReplacePrivateInputAndCompletionClearsCopies()
    {
        ReplayEventEnvelope[] source = [Event(2), Event(1)];
        byte[] second = source[0].Payload;
        byte[] original = second.ToArray();
        IAsyncAggregateReplay replay = Substitute.For<IAsyncAggregateReplay>();
        replay.CanReplayAggregateType("widget").Returns(_ =>
        {
            second[0] = (byte)'!';
            source[0] = Event(50) with { MetadataVersion = 2 };
            return true;
        });
        byte[][]? owned = null;
        replay.ReplayAsync(Arg.Any<AggregateReconstructionRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            AggregateReconstructionRequest captured = call.ArgAt<AggregateReconstructionRequest>(0);
            captured.Events.Select(item => item.SequenceNumber).ShouldBe([1, 2]);
            captured.Events[1].Payload.ShouldBe(original);
            captured.Events[1].Payload.ShouldNotBeSameAs(second);
            captured.Events.ShouldNotBeOfType<ReplayEventEnvelope[]>();
            var slots = (IList<ReplayEventEnvelope>)captured.Events;
            Should.Throw<NotSupportedException>(() => slots[0] = Event(1) with { Payload = second });
            owned = captured.Events.Select(item => item.Payload).ToArray();
            return Task.FromResult(AggregateReconstructionResult.Succeeded("{}", 2));
        });
        using ServiceProvider provider = new ServiceCollection().AddKeyedSingleton("widget", replay).BuildServiceProvider();

        (await DomainServiceRequestRouter.ReplayAsync(provider, Request(source))).Status.ShouldBe(AggregateReconstructionStatus.Succeeded);

        owned.ShouldNotBeNull();
        foreach (byte[] copy in owned) { copy.ShouldAllBe(value => value == 0); }
        second[0].ShouldBe((byte)'!');
        second[1..].ShouldBe(original[1..]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerFailureOrCancellationClearsPrivateInputAndPreservesSource(bool cancelled)
    {
        ReplayEventEnvelope original = Event(1);
        byte[] before = original.Payload.ToArray();
        byte[]? owned = null;
        using var cancellation = new CancellationTokenSource();
        IAsyncAggregateReplay replay = Substitute.For<IAsyncAggregateReplay>();
        replay.CanReplayAggregateType("widget").Returns(true);
        replay.ReplayAsync(Arg.Any<AggregateReconstructionRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            owned = call.ArgAt<AggregateReconstructionRequest>(0).Events[0].Payload;
            if (cancelled) { cancellation.Cancel(); }
            return cancelled
                ? Task.FromResult(AggregateReconstructionResult.Succeeded("{}", 1))
                : Task.FromException<AggregateReconstructionResult>(new InvalidOperationException("fixture failure"));
        });
        using ServiceProvider provider = new ServiceCollection().AddKeyedSingleton("widget", replay).BuildServiceProvider();

        if (cancelled)
        {
            OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(
                () => DomainServiceRequestRouter.ReplayAsync(provider, Request([original]), cancellation.Token));
            error.CancellationToken.ShouldBe(cancellation.Token);
        }
        else
        {
            _ = await Should.ThrowAsync<InvalidOperationException>(
                () => DomainServiceRequestRouter.ReplayAsync(provider, Request([original]), cancellation.Token));
        }

        owned.ShouldNotBeNull();
        owned.ShouldAllBe(value => value == 0);
        original.Payload.ShouldBe(before);
    }

    [Fact]
    public async Task OwnershipCancellationPreservesOriginalTokenAndInvokesNoHandler()
    {
        ReplayEventEnvelope original = Event(1);
        byte[] before = original.Payload.ToArray();
        using var cancellation = new CancellationTokenSource();
        IAsyncAggregateReplay replay = Substitute.For<IAsyncAggregateReplay>();
        replay.CanReplayAggregateType("widget").Returns(_ => { cancellation.Cancel(); return true; });
        using ServiceProvider provider = new ServiceCollection().AddKeyedSingleton("widget", replay).BuildServiceProvider();

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(
            () => DomainServiceRequestRouter.ReplayAsync(provider, Request([original]), cancellation.Token));

        error.CancellationToken.ShouldBe(cancellation.Token);
        _ = replay.DidNotReceiveWithAnyArgs().ReplayAsync(default!, default);
        original.Payload.ShouldBe(before);
    }

    [Fact]
    public async Task SynchronousReplayOwnershipIsIndependentOfCommandRegistration()
    {
        IAggregateReplay replay = Substitute.For<IAggregateReplay>();
        replay.CanReplayAggregateType("widget").Returns(true);
        replay.Replay(Arg.Any<AggregateReconstructionRequest>()).Returns(AggregateReconstructionResult.Succeeded("{}", 0));
        using ServiceProvider provider = new ServiceCollection().AddKeyedSingleton("widget", replay).BuildServiceProvider();

        DomainServiceRequestRouter.Replay(provider, Request([])).Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        (await DomainServiceRequestRouter.ReplayAsync(provider, Request([]))).Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
    }

    [Fact]
    public void AmbiguousSynchronousOwnershipInvokesNoHandler()
    {
        IAggregateReplay first = Substitute.For<IAggregateReplay>();
        IAggregateReplay second = Substitute.For<IAggregateReplay>();
        first.CanReplayAggregateType("widget").Returns(true);
        second.CanReplayAggregateType("widget").Returns(true);
        using ServiceProvider provider = new ServiceCollection()
            .AddKeyedSingleton("widget", first).AddKeyedSingleton("widget", second).BuildServiceProvider();

        DomainServiceRequestRouter.Replay(provider, Request([])).ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnknownAggregateType);

        first.DidNotReceiveWithAnyArgs().Replay(default!);
        second.DidNotReceiveWithAnyArgs().Replay(default!);
    }

    [Fact]
    public async Task ExplicitDerivedAsyncReplayKeepsCustomInterfaceDispatch()
    {
        var aggregate = new CustomAsyncReplayAggregate();
        using ServiceProvider provider = new ServiceCollection()
            .AddKeyedSingleton<IAsyncAggregateReplay>("widget", aggregate).BuildServiceProvider();
        var request = Request([]) with { AggregateType = nameof(CustomAsyncReplayAggregate) };

        (await DomainServiceRequestRouter.ReplayAsync(provider, request)).StateJson.ShouldBe("{\"customAsync\":true}");
    }

    [Fact]
    public void ExplicitDerivedSynchronousReplayKeepsCustomInterfaceDispatch()
    {
        var aggregate = new CustomSyncReplayAggregate();
        using ServiceProvider provider = new ServiceCollection()
            .AddKeyedSingleton<IAggregateReplay>("widget", aggregate).BuildServiceProvider();
        var request = Request([]) with { AggregateType = nameof(CustomSyncReplayAggregate) };

        DomainServiceRequestRouter.Replay(provider, request).StateJson.ShouldBe("{\"customSync\":true}");
    }

    [Fact]
    public async Task BuiltInReplayStillProducesCanonicalStateThroughIndependentRoutes()
    {
        var aggregate = new WidgetAggregate();
        using ServiceProvider provider = new ServiceCollection()
            .AddKeyedSingleton<IAsyncAggregateReplay>("widget", aggregate)
            .AddKeyedSingleton<IAggregateReplay>("widget", aggregate).BuildServiceProvider();
        AggregateReconstructionRequest request = Request([Event(2), Event(1)]);

        DomainServiceRequestRouter.Replay(provider, request).StateJson.ShouldBe("{\"count\":2}");
        (await DomainServiceRequestRouter.ReplayAsync(provider, request)).StateJson.ShouldBe("{\"count\":2}");
        request.Events[0].Payload.ShouldBe("{}"u8.ToArray());
    }

    private static ReplayEventEnvelope Event(long sequence)
        => new(sequence, nameof(WidgetCreated), "{}"u8.ToArray(), "json", 1, "message", null, null);

    private static AggregateReconstructionRequest Request(IReadOnlyList<ReplayEventEnvelope> events)
        => new("tenant", "widget", "widget", "aggregate", 2, events, false, null);
}
