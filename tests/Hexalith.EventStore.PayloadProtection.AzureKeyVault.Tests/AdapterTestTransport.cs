// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 3-7; base V079-V106, V127-V132.
using Azure.Core.Pipeline;
using Azure.Core;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;
internal sealed class AdapterTestTransport(Func<HttpMessage, ValueTask> send) : HttpPipelineTransport
{
    internal int Count { get; private set; }

    public override Request CreateRequest() => new AzureKeyVaultRequest();
    public override void Process(HttpMessage message) => ProcessAsync(message).AsTask().GetAwaiter().GetResult();
    public override ValueTask ProcessAsync(HttpMessage message)
    {
        Count++;
        return send(message);
    }
}
