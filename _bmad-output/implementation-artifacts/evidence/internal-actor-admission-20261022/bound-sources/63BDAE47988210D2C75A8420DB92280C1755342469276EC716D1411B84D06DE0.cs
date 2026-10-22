namespace Hexalith.EventStore.Contracts.Effects;

/// <summary>Persisted target effect outcome.</summary>
public enum TrustedEffectDisposition
{
    /// <summary>The target accepted and emitted events.</summary>
    Success,

    /// <summary>The target emitted a domain rejection.</summary>
    Rejection,

    /// <summary>The target accepted without emitting events.</summary>
    NoOp,
}
