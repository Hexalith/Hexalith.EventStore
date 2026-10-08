namespace Hexalith.EventStore.Server.Events;

/// <summary>Identifies a refused candidate Apply without exposing its mutable graph or private canonical diagnostic.</summary>
/// <param name="sequence">The exact source sequence whose Apply failed.</param>
/// <param name="canonicalType">The admitted current event identity.</param>
/// <param name="cause">The original candidate Apply failure.</param>
internal sealed class DaprLogicalReplayApplyException(long sequence, string canonicalType, Exception cause)
    : InvalidOperationException("ApplyFailed: private candidate failed; no page transition was staged.", cause)
{
    /// <summary>Gets the exact signed source sequence whose Apply failed.</summary>
    internal long Sequence
    {
        get;
    }
    = sequence;
    /// <summary>Gets the allow-listed canonical event identity whose Apply failed.</summary>
    internal string CanonicalType
    {
        get;
    }
    = canonicalType;
}
