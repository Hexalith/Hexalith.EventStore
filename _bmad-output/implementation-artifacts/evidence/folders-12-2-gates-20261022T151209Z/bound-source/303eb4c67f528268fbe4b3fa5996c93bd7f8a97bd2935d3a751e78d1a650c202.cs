namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Separates callback observations and cancellation between concurrently executing tests.</summary>
internal sealed class CancellationTestScope : IDisposable
{
    private static readonly AsyncLocal<CancellationTestScope?> _current = new();
    private readonly CancellationTestScope? _prior = _current.Value;

    /// <summary>Creates an isolated cancellation scope for the current async execution.</summary>
    internal CancellationTestScope() => _current.Value = this;

    /// <summary>Gets the current test's scope.</summary>
    internal static CancellationTestScope Current => _current.Value ?? throw new InvalidOperationException("No cancellation test scope.");

    /// <summary>Gets the original request token source.</summary>
    internal CancellationTokenSource Cancellation { get; } = new();

    /// <summary>Gets or sets the number of completed Apply calls.</summary>
    internal int Applied { get; set; }

    /// <summary>Gets or sets the number of invoked command handlers.</summary>
    internal int Handled { get; set; }

    /// <summary>Gets or sets the Apply ordinal that cancels the request.</summary>
    internal int CancelAfterApply { get; set; }

    /// <summary>Gets or sets whether Apply throws its original cancellation exception.</summary>
    internal bool ThrowInApply { get; set; }

    /// <summary>Gets or sets whether successor serialization cancels the request.</summary>
    internal bool CancelInStateGetter { get; set; }

    /// <summary>Gets or sets whether reading termination state cancels the request.</summary>
    internal bool CancelInTerminationGetter { get; set; }

    /// <summary>Gets or sets the converter outcome: 0=ordinary, 1=value, 2=null, 3=JSON failure, 4=cancellation.</summary>
    internal int ConverterCancellationOutcome { get; set; }

    /// <summary>Gets or sets the callback invoked while an event converter reads its private payload.</summary>
    internal Action? OnConverterRead { get; set; }

    /// <summary>Gets or sets the number of payload converter invocations.</summary>
    internal int ConverterReads { get; set; }

    /// <summary>Gets or sets whether Handle cancels the request.</summary>
    internal bool CancelInHandle { get; set; }

    /// <summary>Gets or sets whether Handle throws its original cancellation exception.</summary>
    internal bool ThrowInHandle { get; set; }

    /// <summary>Gets or sets the token forwarded into the protected processor seam.</summary>
    internal CancellationToken ObservedHandlerToken { get; set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _current.Value = _prior;
        Cancellation.Dispose();
    }
}
