using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Private immutable facade over an exclusively owned payload buffer.</summary>
internal sealed class ImmutablePayload : IReadOnlyPayload, IDisposable {
    private readonly CancellationToken _cancellationToken;
    private byte[]? _owner;
    private readonly object _lifetimeLock = new();
    private EventBufferReservation? _reservation;

    internal ImmutablePayload(byte[] owner, int length, CancellationToken cancellationToken, EventBufferReservation? reservation = null) {
        ArgumentNullException.ThrowIfNull(owner);
        if (length < 0 || length > owner.Length) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        _owner = owner;
        Length = length;
        _cancellationToken = cancellationToken;
        _reservation = reservation;
    }

    public int Length { get; }

    public void CopyTo(int sourceOffset, Span<byte> destination) {
        lock (_lifetimeLock) {
            byte[] owner = _owner ?? throw new ObjectDisposedException(nameof(ImmutablePayload));
            _cancellationToken.ThrowIfCancellationRequested();
            if (sourceOffset < 0 || sourceOffset > Length || destination.Length > Length - sourceOffset) {
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "The requested range is outside the payload.");
            }

            owner.AsSpan(sourceOffset, destination.Length).CopyTo(destination);
            _cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>Hashes the private exact payload without exposing its owner or requiring another copy.</summary>
    internal byte[] ComputeSha256() {
        lock (_lifetimeLock) {
            byte[] owner = _owner ?? throw new ObjectDisposedException(nameof(ImmutablePayload));
            return SHA256.HashData(owner.AsSpan(0, Length));
        }
    }

    /// <summary>Validates one complete strict UTF-8 JSON state value without materializing a document or token strings.</summary>
    internal void RequireJsonState(CancellationToken cancellationToken)
    {
        lock (_lifetimeLock)
        {
            byte[] owner = _owner ?? throw new ObjectDisposedException(nameof(ImmutablePayload));
            cancellationToken.ThrowIfCancellationRequested();
            _ = new UTF8Encoding(false, true).GetCharCount(owner.AsSpan(0, Length));
            var reader = new Utf8JsonReader(owner.AsSpan(0, Length));
            bool present = false;
            while (reader.Read())
            {
                present = true;
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (!present)
            {
                throw new JsonException("Canonical state must contain one complete JSON value.");
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>Creates a charged detached source, clearing partial copies on every failure.</summary>
    internal static ImmutablePayload CopyFrom(IReadOnlyPayload source, EventBufferBudget budget, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(budget);
        cancellationToken.ThrowIfCancellationRequested();
        int length = source.Length;
        if (length is < 0 or > 64 * 1024 * 1024) {
            throw new ArgumentOutOfRangeException(nameof(source), "ReadableLimit: source payload exceeds 64 MiB.");
        }

        EventBufferReservation reservation = budget.Reserve(length);
        byte[]? owner = null;
        try {
            owner = new byte[length];
            source.CopyTo(0, owner);
            cancellationToken.ThrowIfCancellationRequested();
            if (source.Length != length) {
                throw new ArgumentException("The source payload length changed during copying.", nameof(source));
            }

            return new ImmutablePayload(owner, length, cancellationToken, reservation);
        }
        catch {
            if (owner is not null) {
                CryptographicOperations.ZeroMemory(owner);
            }

            reservation.Dispose();
            throw;
        }
    }

    public void Dispose() {
        lock (_lifetimeLock) {
            byte[]? owner = Interlocked.Exchange(ref _owner, null);
            if (owner is not null) {
                CryptographicOperations.ZeroMemory(owner);
            }

            Interlocked.Exchange(ref _reservation, null)?.Dispose();
        }
    }
}
