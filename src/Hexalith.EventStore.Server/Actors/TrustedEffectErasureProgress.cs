namespace Hexalith.EventStore.Server.Actors;

/// <summary>Durable actor-local cursor and terminal fence for a retryable tenant erasure.</summary>
/// <param name="LastSequence">Last event sequence present when erasure began.</param>
/// <param name="NextSequence">Next event sequence to erase.</param>
/// <param name="Completed">Whether all stream and effect evidence was removed.</param>
/// <param name="CapabilityNonce">Nonce consumed by the latest authorized purge turn.</param>
internal sealed record TrustedEffectErasureProgress(
    long LastSequence, long NextSequence, bool Completed, string CapabilityNonce)
{
    /// <summary>Fixed actor-state name.</summary>
    public const string StateName = "effect_erasure_progress";
}
