// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 10.6.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Mutable breaker state owned under the executor gate, keyed by backend and endpoint fingerprint.</summary>
internal sealed class PayloadBackendCircuitState
{
    /// <summary>Gets the bounded window of transient attempt timestamps.</summary>
    internal Queue<long> Failures { get; } = new();
    /// <summary>Gets or sets the beginning of the current open interval.</summary>
    internal long? OpenedAt { get; set; }
    /// <summary>Gets or sets the current open interval, capped at five minutes.</summary>
    internal TimeSpan OpenInterval { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Gets or sets whether the single half-open probe is in flight.</summary>
    internal bool ProbeRunning { get; set; }
}
