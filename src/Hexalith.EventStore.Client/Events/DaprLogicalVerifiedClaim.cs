using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Retains decoding and private evidence capacity through the caller's verified-claim consumption.</summary>
/// <typeparam name="T">The strictly decoded logical claim type.</typeparam>
internal sealed class DaprLogicalVerifiedClaim<T> : IDisposable where T : class
{
    private T? _value;
    private byte[]? _image;
    private EventBufferReservation? _charge;

    /// <summary>Takes ownership of decoded fields, exact private claim bytes and their retained capacity.</summary>
    internal DaprLogicalVerifiedClaim(T value, byte[] image, EventBufferReservation charge) { _value = value; _image = image; _charge = charge; }

    /// <summary>Gets the verified fields only during their owned lifetime.</summary>
    internal T Value => _value ?? throw new ObjectDisposedException(nameof(DaprLogicalVerifiedClaim<T>));

    /// <inheritdoc/>
    public void Dispose()
    {
        _value = null; byte[]? image = Interlocked.Exchange(ref _image, null);
        if (image is not null) { CryptographicOperations.ZeroMemory(image); }
        Interlocked.Exchange(ref _charge, null)?.Dispose();
    }
}
