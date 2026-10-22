using PactNet.Infrastructure.Outputters;

namespace Hexalith.EventStore.ProviderVerification;

internal sealed class DiscardingPactOutput : IOutput
{
    public void WriteLine(string line)
    {
        // Native verifier output can contain request/response bodies and is intentionally discarded.
    }
}
