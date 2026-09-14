namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Records buffer-clear callbacks and fails immediately if any observed byte is nonzero.
/// </summary>
internal sealed class RecordingBufferObserver(Action<SensitiveBufferKind>? afterClear = null) : ISensitiveBufferObserver
{
    private readonly Lock _sync = new();

    /// <summary>Gets observed buffer categories.</summary>
    internal List<SensitiveBufferKind> Observed { get; } = [];

    /// <inheritdoc/>
    public void BufferCleared(SensitiveBufferKind kind, ReadOnlySpan<byte> buffer)
    {
        buffer.ToArray().ShouldAllBe(static value => value == 0);
        lock (_sync)
        {
            Observed.Add(kind);
        }

        afterClear?.Invoke(kind);
    }
}
