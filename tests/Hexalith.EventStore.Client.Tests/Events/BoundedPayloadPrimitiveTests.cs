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
}
