// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 5, 11, 15.
using System.Security.Cryptography;
using Azure.Core;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Owns bounded UTF-8 crypto content until actual transport completion, then clears it.</summary>
internal sealed class AzureKeyVaultOwnedRequestContent(byte[] bytes, Action<byte[]>? cleared = null) : RequestContent
{
    private byte[]? _bytes = bytes;
    /// <inheritdoc/>
    public override bool TryComputeLength(out long length)
    {
        length = _bytes?.Length ?? 0;
        return true;
    }

    /// <inheritdoc/>
    public override void WriteTo(Stream stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        stream.Write(_bytes ?? throw new ObjectDisposedException(nameof(AzureKeyVaultOwnedRequestContent)));
    }

    /// <inheritdoc/>
    public override async Task WriteToAsync(Stream stream, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(_bytes ?? throw new ObjectDisposedException(nameof(AzureKeyVaultOwnedRequestContent)), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        byte[]? owned = Interlocked.Exchange(ref _bytes, null);
        if (owned is not null)
        {
            CryptographicOperations.ZeroMemory(owned);
            cleared?.Invoke(owned);
        }
    }
}
