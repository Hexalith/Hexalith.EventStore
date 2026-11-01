namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Clear observers cannot interrupt plaintext ownership transfer or disposal.</summary>
public sealed class SerializationBufferTests
{
    [Fact]
    public void ThrowingClearObserversCannotLeakGrowthOrDisposedBuffers()
    {
        var observer = new RecordingBufferObserver(_ => throw new InvalidOperationException("observer-private-canary"));
        using var buffer = new PayloadSerializationBuffer(observer);
        Memory<byte> original = buffer.GetMemory(128);
        original.Span[..128].Fill(0x53); buffer.Advance(128);
        Memory<byte> replacement = buffer.GetMemory(1024);
        original.Span.ToArray().ShouldAllBe(value => value == 0);
        buffer.ToArray().ShouldAllBe(value => value == 0x53);
        replacement.Span[..128].Fill(0x69); buffer.Advance(128);
        buffer.Dispose();
        replacement.Span.ToArray().ShouldAllBe(value => value == 0);
        buffer.ToArray().ShouldBeEmpty();
        observer.Observed.Count.ShouldBeGreaterThanOrEqualTo(3);
    }
}
