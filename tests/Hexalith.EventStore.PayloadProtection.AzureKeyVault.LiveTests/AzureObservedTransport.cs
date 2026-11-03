// Approved Azure exception sections 3 and 7; observes the real transport without synthesizing responses.
using Azure.Core.Pipeline;
using Azure.Core;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests;
internal sealed class AzureObservedTransport(HttpPipelineTransport inner) : HttpPipelineTransport
{
    internal List<int> CryptoStatuses { get; } = [];
    internal bool CryptoDispatched { get; private set; }
    internal int Calls { get; private set; }

    public override Request CreateRequest() => inner.CreateRequest();
    public override void Process(HttpMessage message) => ProcessAsync(message).AsTask().GetAwaiter().GetResult();
    public override async ValueTask ProcessAsync(HttpMessage message)
    {
        Calls++;
        try
        {
            await inner.ProcessAsync(message);
        }
        finally
        {
            if (message.Request.Method == RequestMethod.Post)
            {
                CryptoDispatched |= message.Request is AzureKeyVaultRequest { Dispatched: true };
                if (message.HasResponse)
                {
                    CryptoStatuses.Add(message.Response.Status);
                }
            }
        }
    }
}
