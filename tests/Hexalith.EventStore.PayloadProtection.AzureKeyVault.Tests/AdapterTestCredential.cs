// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 3-7; base V079-V106, V127-V132.
using Azure.Core;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;
internal sealed class AdapterTestCredential(string token) : TokenCredential
{
    internal int Count { get; private set; }
    internal TokenRequestContext LastContext { get; private set; }
    internal Exception? Failure { get; set; }
    internal Func<TokenRequestContext, CancellationToken, ValueTask<AccessToken>>? Acquire { get; set; }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();
    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Count++;
        LastContext = requestContext;
        if (Failure is not null)
        {
            throw Failure;
        }

        if (Acquire is not null)
        {
            return Acquire(requestContext, cancellationToken);
        }

        return ValueTask.FromResult(new AccessToken(token, DateTimeOffset.UtcNow.AddHours(2)));
    }
}
