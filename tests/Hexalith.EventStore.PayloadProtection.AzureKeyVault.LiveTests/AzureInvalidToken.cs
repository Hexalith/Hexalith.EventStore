// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 5-7; separately authorized isolated environment required.
using Azure.Core;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests;
/// <summary>Authorized 401 injection only; runtime construction always uses managed identity.</summary>
internal sealed class AzureInvalidToken : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("invalid-conformance-token", DateTimeOffset.UtcNow.AddMinutes(1));
    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => ValueTask.FromResult(GetToken(requestContext, cancellationToken));
}
