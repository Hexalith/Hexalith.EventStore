namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Records buffer-clear callbacks for assertions after the best-effort production callback returns.
/// </summary>
internal sealed class RecordingBufferObserver(Action<SensitiveBufferKind>? afterClear = null) : ISensitiveBufferObserver
{
    private readonly List<(SensitiveBufferKind Kind, bool WasZero, int Length)> _observations = [];
    private readonly Lock _sync = new();

    /// <summary>Gets a locked snapshot of observed, verified-zero buffer categories.</summary>
    internal IReadOnlyList<SensitiveBufferKind> Observed
    {
        get
        {
            lock (_sync)
            {
                _observations.ShouldAllBe(
                    static observation => observation.WasZero,
                    "Every production clear callback must expose an already-zeroed buffer.");
                return [.. _observations.Select(static observation => observation.Kind)];
            }
        }
    }

    /// <inheritdoc/>
    public void BufferCleared(SensitiveBufferKind kind, ReadOnlySpan<byte> buffer)
    {
        bool wasZero = true;
        foreach (byte value in buffer)
        {
            wasZero &= value == 0;
        }

        lock (_sync)
        {
            _observations.Add((kind, wasZero, buffer.Length));
        }

        afterClear?.Invoke(kind);
    }
}
