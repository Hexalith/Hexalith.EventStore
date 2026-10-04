namespace Hexalith.EventStore.Contracts.Events;

/// <summary>Writes a bounded payload without exposing its backing storage.</summary>
public interface IBoundedPayloadWriter {
    /// <summary>Writes bytes after charging the required capacity.</summary>
    /// <param name="bytes">The bytes to append.</param>
    void Write(ReadOnlySpan<byte> bytes);

    /// <summary>Completes the write once without returning the backing storage.</summary>
    void Complete();
}
