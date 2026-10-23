// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base section 10.6.
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
