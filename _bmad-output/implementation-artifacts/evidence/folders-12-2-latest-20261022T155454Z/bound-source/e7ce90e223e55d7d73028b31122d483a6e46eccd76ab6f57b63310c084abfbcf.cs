namespace Hexalith.EventStore.Server.Commands;

/// <summary>Provides one validated active-writer and retained-reader digest-key snapshot.</summary>
public interface IIdempotencyDigestKeyProvider
{
    /// <summary>Gets a disposable key-ring snapshot without exposing key material in diagnostics.</summary>
    ValueTask<IdempotencyDigestKeyRing> GetKeyRingAsync(CancellationToken cancellationToken = default);
}
