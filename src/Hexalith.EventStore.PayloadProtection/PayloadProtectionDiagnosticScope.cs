// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 10.8 and 15.
using System.Diagnostics;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Emits only closed activity/metric identities and monotonic elapsed time for one mechanics operation.</summary>
internal sealed class PayloadProtectionDiagnosticScope : IDisposable
{
    private readonly PayloadProtectionOperation _operation;
    private readonly Activity? _activity;
    private readonly Activity? _ambient;
    private readonly long _started = Stopwatch.GetTimestamp();
    private PayloadProtectionDiagnosticResult _result = PayloadProtectionDiagnosticResult.Unavailable;

    /// <summary>Starts the exact closed activity name.</summary>
    internal PayloadProtectionDiagnosticScope(PayloadProtectionOperation operation)
    {
        _operation = operation;
        _activity = PayloadProtectionDiagnostics.Start(operation, out _ambient);
    }
    /// <summary>Sets only a closed outcome without exception or payload details.</summary>
    internal void Complete(PayloadProtectionDiagnosticResult result = PayloadProtectionDiagnosticResult.Success) => _result = result;
    /// <summary>Records elapsed time and restores the prior ambient activity even if observers fail.</summary>
    public void Dispose()
    {
        PayloadProtectionDiagnostics.Stop(_activity, _result, _ambient);
        PayloadProtectionDiagnostics.Record(_operation, _result, Stopwatch.GetElapsedTime(_started).TotalMilliseconds);
    }
}
