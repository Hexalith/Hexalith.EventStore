using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.DomainService;

/// <summary>Charges the encoded cap before copying into the sole cleared write window.</summary>
internal sealed class BoundedV1WireWindowWriter(Stream target, CancellationToken token) : IDisposable
{
    private readonly byte[] _window = new byte[64 * 1024];
    private int _length;
    private long _total;

    /// <summary>Appends framed bytes under the complete result limit.</summary>
    internal async Task RawAsync(ReadOnlyMemory<byte> bytes)
    {
        token.ThrowIfCancellationRequested();
        if (checked(_total + bytes.Length) > 128L * 1024 * 1024)
        {
            throw new InvalidOperationException("ResultLimit: complete encoded V1 output exceeds 128 MiB.");
        }
        _total += bytes.Length;
        while (!bytes.IsEmpty)
        {
            int copy = Math.Min(bytes.Length, _window.Length - _length);
            bytes.Span[..copy].CopyTo(_window.AsSpan(_length));
            _length += copy;
            bytes = bytes[copy..];
            if (_length == _window.Length) { await FinishAsync().ConfigureAwait(false); }
        }
    }

    /// <summary>Escapes Unicode scalars incrementally using the approved JSON string grammar.</summary>
    internal async Task StringAsync(string text)
    {
        await RawAsync("\""u8.ToArray()).ConfigureAwait(false);
        byte[] scalar = new byte[6];
        try
        {
            for (int offset = 0; offset < text.Length;)
            {
                token.ThrowIfCancellationRequested();
                OperationStatus status = Rune.DecodeFromUtf16(text.AsSpan(offset), out Rune rune, out int consumed);
                if (status != OperationStatus.Done) { throw new ArgumentException("Invalid Unicode cannot enter the V1 renderer."); }
                offset += consumed;
                int length;
                if (rune.Value is 34 or 92)
                {
                    scalar[0] = (byte)'\\'; scalar[1] = (byte)rune.Value; length = 2;
                }
                else if (rune.Value < 32)
                {
                    scalar[0] = (byte)'\\'; scalar[1] = (byte)'u'; scalar[2] = (byte)'0'; scalar[3] = (byte)'0';
                    const string hex = "0123456789abcdef";
                    scalar[4] = (byte)hex[rune.Value >> 4]; scalar[5] = (byte)hex[rune.Value & 15]; length = 6;
                }
                else { length = rune.EncodeToUtf8(scalar); }
                await RawAsync(scalar.AsMemory(0, length)).ConfigureAwait(false);
            }
            await RawAsync("\""u8.ToArray()).ConfigureAwait(false);
        }
        finally { CryptographicOperations.ZeroMemory(scalar); }
    }

    /// <summary>Encodes aligned payload chunks without a whole Base64 string or array.</summary>
    internal async Task Base64Async(byte[] payload)
    {
        byte[] encoded = new byte[20_480];
        try
        {
            for (int offset = 0; offset < payload.Length;)
            {
                token.ThrowIfCancellationRequested();
                int take = Math.Min(15_360, payload.Length - offset);
                OperationStatus status = Base64.EncodeToUtf8(payload.AsSpan(offset, take), encoded, out int consumed, out int written);
                if (status != OperationStatus.Done || consumed != take) { throw new InvalidOperationException("Base64 encoding failed."); }
                await RawAsync(encoded.AsMemory(0, written)).ConfigureAwait(false);
                offset += take;
            }
        }
        finally { CryptographicOperations.ZeroMemory(encoded); }
    }

    /// <summary>Flushes a bounded window with the originating request token.</summary>
    internal async Task FinishAsync()
    {
        token.ThrowIfCancellationRequested();
        if (_length > 0)
        {
            await target.WriteAsync(_window.AsMemory(0, _length), token).ConfigureAwait(false);
            CryptographicOperations.ZeroMemory(_window.AsSpan(0, _length));
            _length = 0;
        }
    }

    /// <inheritdoc/>
    public void Dispose() => CryptographicOperations.ZeroMemory(_window);
}
