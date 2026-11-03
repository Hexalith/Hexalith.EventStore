// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 11 and 15.
using System.Security.Cryptography;
using Azure.Core;
using Azure;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Owns the bounded response bytes and clears them on disposal; carries no private reason or request IDs.</summary>
internal sealed class AzureKeyVaultOwnedResponse : Response
{
    private byte[]? _bytes;
    private readonly Dictionary<string, string> _headers;
    /// <summary>Creates a response from transferred owned bytes.</summary>
    internal AzureKeyVaultOwnedResponse(int status, byte[] bytes, Dictionary<string, string> headers)
    {
        Status = status;
        _bytes = bytes;
        _headers = headers;
        ContentStream = new MemoryStream(bytes, false);
    }

    /// <inheritdoc/>
    public override int Status { get; }
    /// <inheritdoc/>
    public override string ReasonPhrase => "";
    /// <inheritdoc/>
    public override Stream? ContentStream { get; set; }
    /// <inheritdoc/>
    public override string ClientRequestId { get; set; } = "";

    /// <inheritdoc/>
    protected override bool TryGetHeader(string name, out string value) => _headers.TryGetValue(name, out value!);
    /// <inheritdoc/>
    protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
    {
        if (_headers.TryGetValue(name, out string? value))
        {
            values = [value];
            return true;
        }

        values = null!;
        return false;
    }

    /// <inheritdoc/>
    protected override bool ContainsHeader(string name) => _headers.ContainsKey(name);
    /// <inheritdoc/>
    protected override IEnumerable<HttpHeader> EnumerateHeaders() => _headers.Where(pair => !pair.Key.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase)).Select(pair => new HttpHeader(pair.Key, pair.Value));
    /// <inheritdoc/>
    public override void Dispose()
    {
        ContentStream?.Dispose();
        ContentStream = null;
        byte[]? bytes = Interlocked.Exchange(ref _bytes, null);
        if (bytes is not null)
        {
            CryptographicOperations.ZeroMemory(bytes);
        }

        _headers.Clear();
    }

    /// <summary>Returns a safe type label.</summary>
    public override string ToString() => nameof(AzureKeyVaultOwnedResponse);
}
