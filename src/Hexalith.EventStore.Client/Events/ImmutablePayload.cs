using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Private immutable facade over an exclusively owned payload buffer.</summary>
internal sealed class ImmutablePayload : IReadOnlyPayload, IDisposable {
    private readonly CancellationToken _cancellationToken;
    private byte[]? _owner;

    internal ImmutablePayload(byte[] owner, int length, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(owner);
        if (length < 0 || length > owner.Length) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        _owner = owner;
        Length = length;
        _cancellationToken = cancellationToken;
    }

    public int Length { get; }

    public void CopyTo(int sourceOffset, Span<byte> destination) {
        byte[] owner = _owner ?? throw new ObjectDisposedException(nameof(ImmutablePayload));
        _cancellationToken.ThrowIfCancellationRequested();
        if (sourceOffset < 0 || sourceOffset > Length || destination.Length > Length - sourceOffset) {
            throw new ArgumentOutOfRangeException(nameof(sourceOffset), "The requested range is outside the payload.");
        }

        owner.AsSpan(sourceOffset, destination.Length).CopyTo(destination);
        _cancellationToken.ThrowIfCancellationRequested();
    }

    public void Dispose() {
        byte[]? owner = Interlocked.Exchange(ref _owner, null);
        if (owner is not null) {
            CryptographicOperations.ZeroMemory(owner);
        }
    }
}
