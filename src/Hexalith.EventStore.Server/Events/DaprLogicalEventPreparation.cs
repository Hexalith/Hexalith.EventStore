using System.Security.Cryptography;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Owns charged readable input and metadata until the complete logical page can enter resolution.</summary>
/// <remarks>Stored payloads remain actor values; a private digest detects changes without rewriting them.</remarks>
internal sealed class DaprLogicalEventPreparation : IDisposable
{
    private readonly byte[] _storedHash;
    private ImmutablePayload? _readable;
    private EventBufferReservation? _metadataReservation;

    /// <summary>Takes exclusive ownership of readable bytes, their source hash and metadata reservation.</summary>
    internal DaprLogicalEventPreparation(EventEnvelope source, string readableFormat, ImmutablePayload readable,
        byte[] storedHash, EventBufferReservation metadataReservation)
    {
        Source = source;
        ReadableFormat = readableFormat;
        ReadablePayloadLength = readable.Length;
        _readable = readable;
        _storedHash = storedHash;
        _metadataReservation = metadataReservation;
    }

    /// <summary>Gets the privately snapshotted metadata and unchanged actor payload.</summary>
    internal EventEnvelope Source { get; }

    /// <summary>Gets the admitted post-unprotection serialization format.</summary>
    internal string ReadableFormat { get; }

    /// <summary>Gets the source readable size independently of any smaller upcast result.</summary>
    internal int ReadablePayloadLength { get; }

    /// <summary>Refuses source mutation before catalog entry and before page ownership transfer.</summary>
    internal void RequireStoredUnchanged()
    {
        byte[] current = SHA256.HashData(Source.Payload);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(_storedHash, current))
            {
                throw new InvalidOperationException("AddressMismatch: actor event bytes changed during logical resolution.");
            }
        }
        finally { CryptographicOperations.ZeroMemory(current); }
    }

    /// <summary>Transfers the sole readable owner into the shared resolver exactly once.</summary>
    internal ImmutablePayload TakeReadablePayload()
        => Interlocked.Exchange(ref _readable, null)
            ?? throw new InvalidOperationException("ReplayRestartRequired: prepared readable ownership was already transferred.");

    /// <summary>Transfers retained metadata accounting into the resolved view exactly once.</summary>
    internal EventBufferReservation TakeMetadataReservation()
        => Interlocked.Exchange(ref _metadataReservation, null)
            ?? throw new InvalidOperationException("ReplayRestartRequired: prepared metadata ownership was already transferred.");

    /// <inheritdoc/>
    public void Dispose()
    {
        Interlocked.Exchange(ref _readable, null)?.Dispose();
        Interlocked.Exchange(ref _metadataReservation, null)?.Dispose();
        CryptographicOperations.ZeroMemory(_storedHash);
    }
}
