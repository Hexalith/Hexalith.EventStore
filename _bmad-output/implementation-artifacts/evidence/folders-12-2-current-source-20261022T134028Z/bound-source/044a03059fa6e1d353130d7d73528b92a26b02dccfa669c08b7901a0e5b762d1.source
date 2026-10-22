namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Mutable state with a shared Apply method used by command replay and capture.</summary>
public sealed class SnapshotCaptureTestState
{
    /// <summary>Gets or sets the count.</summary>
    public int Count { get; set; }

    /// <summary>Gets or sets a scalar used to verify input-based encoder admission.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Forces the capture Apply callback to fail after receiving its detached input.</summary>
    public bool ThrowOnApply { get; set; }

    /// <summary>Applies an admitted event and mutates its detached input.</summary>
    public void Apply(SnapshotCaptureTestEvent payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Count += payload.Amount;
        payload.Amount = 500;
        if (ThrowOnApply) { throw new InvalidOperationException("test capture failure"); }
    }

    /// <summary>Applies a persisted domain rejection through the same Apply table.</summary>
    public void Apply(SnapshotCaptureTestRejectionEvent payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Count += payload.Amount;
    }
}
