using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>An owner-constructed result; consumers cannot construct an authoritative success receipt.</summary>
public sealed class NativeSourceScanResult<T>
{
    /// <summary>Constructs an owner-verified observation or a refusal without releasing unverified facts.</summary>
    internal NativeSourceScanResult(T? value, NativeSourceObservation? observation, string? reasonCode)
    {
        Value = value;
        Observation = observation;
        ReasonCode = reasonCode;
    }

    /// <summary>Gets selected facts only for a verified current native observation.</summary>
    public T? Value { get; }

    /// <summary>Gets the exact independently verified native revision and final observation instant.</summary>
    public NativeSourceObservation? Observation { get; }

    /// <summary>Gets the safe refusal code, or null after successful verification.</summary>
    public string? ReasonCode { get; }
}
