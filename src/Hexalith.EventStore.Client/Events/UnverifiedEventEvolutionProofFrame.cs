using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Owns the exact admitted outer framing bytes; its claims remain unverified.</summary>
/// <remarks>
/// This dormant local owner establishes no signature, purpose, key authority, scope,
/// sequence, prefix, checkpoint or source truth. It is not a verified event/proof view.
/// No production route accepts it. Semantic claim verification must precede consumers.
/// </remarks>
internal sealed class UnverifiedEventEvolutionProofFrame : IDisposable
{
    private readonly byte[] _bytes;
    private readonly EventBufferReservation _reservation;
    private readonly EventEvolutionProofEntrySlice[] _routes;
    private readonly EventEvolutionProofEntrySlice _prefix;
    private readonly EventEvolutionProofEntrySlice? _checkpoint;
    private bool _disposed;

    internal UnverifiedEventEvolutionProofFrame(byte[] bytes, EventBufferReservation reservation,
        EventEvolutionProofEntrySlice[] routes, EventEvolutionProofEntrySlice prefix, EventEvolutionProofEntrySlice? checkpoint)
    {
        _bytes = bytes;
        _reservation = reservation;
        _routes = routes;
        _prefix = prefix;
        _checkpoint = checkpoint;
    }

    internal int RouteCount { get { ObjectDisposedException.ThrowIf(_disposed, this); return _routes.Length; } }
    internal bool HasCheckpoint { get { ObjectDisposedException.ThrowIf(_disposed, this); return _checkpoint.HasValue; } }
    internal ReadOnlySpan<byte> ExactBytes { get { ObjectDisposedException.ThrowIf(_disposed, this); return _bytes; } }
    internal ReadOnlySpan<byte> RouteClaim(int index) => Claim(_routes[index]);
    internal ReadOnlySpan<byte> RouteKey(int index) => Key(_routes[index]);
    internal ReadOnlySpan<byte> RouteSignature(int index) => Signature(_routes[index]);
    internal ReadOnlySpan<byte> PrefixClaim => Claim(_prefix);
    internal ReadOnlySpan<byte> PrefixKey => Key(_prefix);
    internal ReadOnlySpan<byte> PrefixSignature => Signature(_prefix);
    internal ReadOnlySpan<byte> CheckpointClaim => Claim(_checkpoint ?? throw new InvalidOperationException("No checkpoint entry is present."));
    internal ReadOnlySpan<byte> CheckpointKey => Key(_checkpoint ?? throw new InvalidOperationException("No checkpoint entry is present."));
    internal ReadOnlySpan<byte> CheckpointSignature => Signature(_checkpoint ?? throw new InvalidOperationException("No checkpoint entry is present."));

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        CryptographicOperations.ZeroMemory(_bytes);
        _reservation.Dispose();
    }

    private ReadOnlySpan<byte> Claim(EventEvolutionProofEntrySlice entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _bytes.AsSpan(entry.ClaimOffset, entry.ClaimLength);
    }
    private ReadOnlySpan<byte> Key(EventEvolutionProofEntrySlice entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _bytes.AsSpan(entry.KeyOffset, entry.KeyLength);
    }
    private ReadOnlySpan<byte> Signature(EventEvolutionProofEntrySlice entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _bytes.AsSpan(entry.SignatureOffset, 64);
    }
}
