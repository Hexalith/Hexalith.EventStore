using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Streams;

public sealed class AuthoritativeEventStreamReaderTests
{
    [Fact]
    public async Task CompleteStablePrefix_ReturnsOneCoherentObservation()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Page(call.Arg<StreamReadRequest>(), 2, call.Arg<StreamReadRequest>().ToSequence == 0 ? [] : [Event(1), Event(2)]));
        var reader = new AuthoritativeEventStreamReader(gateway, TimeProvider.System);
        AuthoritativeStreamReadResult result = await reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        result.IsAuthoritative.ShouldBeTrue();
        result.Stream!.Head.ShouldBe(2);
        result.Stream.Events.Select(item => item.SequenceNumber).ShouldBe([1L, 2L]);
        result.Stream.ObservationId.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    public async Task GapReorderOrDuplicate_Denies(long first, long second)
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Page(call.Arg<StreamReadRequest>(), 2, call.Arg<StreamReadRequest>().ToSequence == 0 ? [] : [Event(first), Event(second)]));
        (await new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken)).IsAuthoritative.ShouldBeFalse();
    }

    [Fact]
    public async Task ChangedHead_NeverReturnsMixedObservation()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        int calls = 0;
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                int current = ++calls;
                long head = ((current - 1) / 3) + 1 + (current % 3 == 0 ? 1 : 0);
                StreamReadRequest request = call.Arg<StreamReadRequest>();
                return Page(request, head, request.ToSequence == 0 ? [] : Enumerable.Range(1, (int)request.ToSequence!).Select(value => Event(value)).ToArray());
            });
        AuthoritativeStreamReadResult result = await new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        result.FailureReason.ShouldBe("source-head-changed");
        calls.ShouldBe(9);
    }

    [Fact]
    public async Task WholeReadDeadline_StopsUncooperativeGateway()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource<StreamReadPage>().Task);
        var reader = new AuthoritativeEventStreamReader(gateway, TimeProvider.System, TimeSpan.FromMilliseconds(20));
        (await reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken))
            .FailureReason.ShouldBe("source-time-bound-exceeded");
    }

    [Fact]
    public async Task CallerCancellation_Propagates()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), canceled.Token));
    }

    [Fact]
    public async Task ForeignPageOrExcessiveHead_Denies()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Page(call.Arg<StreamReadRequest>(), 10_001, []));
        (await new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken)).IsAuthoritative.ShouldBeFalse();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Page(call.Arg<StreamReadRequest>(), 0, []) with { Tenant = "tenant-b" });
        (await new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken)).IsAuthoritative.ShouldBeFalse();
    }

    private static StreamReadEvent Event(long sequence) => new(sequence, "Created", "{}"u8.ToArray(), "json", 1, "message", null, null, DateTimeOffset.UnixEpoch, "actor");
    private static StreamReadPage Page(StreamReadRequest request, long head, IReadOnlyList<StreamReadEvent> events)
        => new(request.Tenant, request.Domain, request.AggregateId, events,
            new(request.FromSequence, request.ToSequence, events.Count == 0 ? null : events[^1].SequenceNumber, head, events.Count, false, null));
}
