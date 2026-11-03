// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 5, 11, 15.
using Azure.Core.Pipeline;
using Azure.Core;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Uses the public fixed-scope bearer policy while forbidding independent CAE authentication/replay of a crypto 401.</summary>
internal sealed class AzureKeyVaultNoChallengePolicy(TokenCredential credential, string scope) : BearerTokenAuthenticationPolicy(credential, scope)
{
    /// <inheritdoc/>
    protected override ValueTask<bool> AuthorizeRequestOnChallengeAsync(HttpMessage message) => ValueTask.FromResult(false);
    /// <inheritdoc/>
    protected override bool AuthorizeRequestOnChallenge(HttpMessage message) => false;
}
