using Hexalith.EventStore.Client.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

public sealed class BoundedPayloadPrimitiveTests {
    [Fact]
    public void Writer_SealsAndCopiesOutputWithoutExposingItsOwner() {
        using var writer = new BoundedPayloadWriter(8, CancellationToken.None);
        writer.Write([1, 2]);
        writer.Write([3, 4]);
        writer.Complete();

        using ImmutablePayload payload = writer.TakeCompletedPayload();
        byte[] copy = new byte[payload.Length];
        payload.CopyTo(0, copy);

        copy.ShouldBe([1, 2, 3, 4]);
        Should.Throw<InvalidOperationException>(() => writer.Write([5]));
        Should.Throw<InvalidOperationException>(() => writer.TakeCompletedPayload());
    }

    [Fact]
    public void Writer_RejectsOverflowAndRepeatedCompletion() {
        using var writer = new BoundedPayloadWriter(2, CancellationToken.None);
        writer.Write([1, 2]);

        Should.Throw<InvalidOperationException>(() => writer.Write([3]));
        writer.Complete();
        Should.Throw<InvalidOperationException>(() => writer.Complete());
    }

    [Fact]
    public void ImmutablePayload_RejectsInvalidRangesAndUseAfterDispose() {
        using var writer = new BoundedPayloadWriter(2, CancellationToken.None);
        writer.Write([1, 2]);
        writer.Complete();
        ImmutablePayload payload = writer.TakeCompletedPayload();

        Should.Throw<ArgumentOutOfRangeException>(() => payload.CopyTo(1, new byte[2]));
        payload.Dispose();
        Should.Throw<ObjectDisposedException>(() => payload.CopyTo(0, new byte[1]));
    }

    [Fact]
    public void ScratchAllocator_ProvidesExactCapacityAndChecksCancellation() {
        var allocator = new BoundedScratchAllocator(16);
        int observedLength = -1;
        allocator.WithScratch(7, span => {
            observedLength = span.Length;
            span.Fill(0x7f);
        }, CancellationToken.None);

        observedLength.ShouldBe(7);
        Should.Throw<ArgumentOutOfRangeException>(() => allocator.WithScratch(17, _ => { }, CancellationToken.None));
        using var source = new CancellationTokenSource();
        source.Cancel();
        Should.Throw<OperationCanceledException>(() => allocator.WithScratch(1, _ => { }, source.Token));
    }

    [Fact]
    public void ScratchAllocator_ZeroesInitialBytesAndRefusesNestedOverReservation() {
        var allocator = new BoundedScratchAllocator(8);
        allocator.WithScratch(5, outer => {
            outer.ToArray().ShouldBe(new byte[5]);
            Should.Throw<InvalidOperationException>(() => allocator.WithScratch(4, _ => { }, CancellationToken.None));
            allocator.WithScratch(3, inner => inner.ToArray().ShouldBe(new byte[3]), CancellationToken.None);
            outer.Fill(0xff);
        }, CancellationToken.None);

        allocator.WithScratch(8, span => span.ToArray().ShouldBe(new byte[8]), CancellationToken.None);
    }

    [Fact]
    public async Task ScratchAllocator_RefusesConcurrentOverReservationAndReleasesAfterFailure() {
        var allocator = new BoundedScratchAllocator(8);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Task occupied = Task.Run(() => allocator.WithScratch(8, span => {
            span.Fill(1);
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        }, CancellationToken.None));
        try {
            entered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            Should.Throw<InvalidOperationException>(() => allocator.WithScratch(1, _ => { }, CancellationToken.None));
        }
        finally {
            release.Set();
            await occupied;
        }

        Should.Throw<FormatException>(() => allocator.WithScratch(8, _ => throw new FormatException(), CancellationToken.None));
        allocator.WithScratch(8, span => span.ToArray().ShouldBe(new byte[8]), CancellationToken.None);
    }

    [Fact]
    public void ScratchAllocator_CapturesOriginalCancellationAndInvalidatesItsInvocation() {
        using var cancellation = new CancellationTokenSource();
        var budget = new EventBufferBudget(8);
        using var allocator = new BoundedScratchAllocator(8, budget, cancellation.Token);

        OperationCanceledException failure = Should.Throw<OperationCanceledException>(() => allocator.WithScratch(8,
            bytes => { bytes.Fill(1); cancellation.Cancel(); }, CancellationToken.None));

        failure.CancellationToken.ShouldBe(cancellation.Token);
        budget.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task ScratchAllocator_RetainedRunningCallbackPreventsInvocationAcceptance() {
        var budget = new EventBufferBudget(8);
        using var allocator = new BoundedScratchAllocator(8, budget);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Task callback = Task.Run(() => allocator.WithScratch(8, bytes => {
            bytes.Fill(1);
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        }, CancellationToken.None));
        try {
            entered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            allocator.Dispose();
            budget.LiveBytes.ShouldBe(8);
            Should.Throw<InvalidOperationException>(allocator.RequireValidInvocation);
            Should.Throw<ObjectDisposedException>(() => allocator.WithScratch(0, _ => { }, CancellationToken.None));
        }
        finally {
            release.Set();
            await callback;
        }

        budget.LiveBytes.ShouldBe(0);
    }
}
