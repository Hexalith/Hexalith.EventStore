// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 11 and 15.
using Azure.Core;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Stores only a single bounded request and its constructive dispatch marker; never formats provider information.</summary>
internal sealed class AzureKeyVaultRequest : Request
{
    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Gets or sets whether bytes may have reached the provider.</summary>
    internal bool Dispatched { get; set; }
    /// <inheritdoc/>
    public override string ClientRequestId { get; set; } = "";

    /// <inheritdoc/>
    protected override void AddHeader(string name, string value)
    {
        if (!_headers.TryAdd(name, value))
        {
            _headers[name] += "," + value;
        }
    }

    /// <inheritdoc/>
    protected override void SetHeader(string name, string value) => _headers[name] = value;
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
    protected override bool RemoveHeader(string name) => _headers.Remove(name);
    /// <inheritdoc/>
    protected override IEnumerable<HttpHeader> EnumerateHeaders() => _headers.Select(pair => new HttpHeader(pair.Key, pair.Value));
    /// <inheritdoc/>
    public override void Dispose()
    {
        Content?.Dispose();
        Content = null;
        _headers.Clear();
    }

    /// <summary>Returns a safe type label.</summary>
    public override string ToString() => nameof(AzureKeyVaultRequest);
}
