// Test-only token observation; delegates to the same deterministic runtime credential and adds no provider calls.
using Azure.Core;
using Hexalith.EventStore.PayloadProtection.AzureKeyVault.Conformance;
using System.Collections.Concurrent;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests;
internal sealed class AzureLiveCredential(TokenCredential inner, string? selectedClientId) : TokenCredential
{
    internal static ConcurrentBag<string> Tokens { get; } = [];

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        using var selectedIdentity = new AzureIdentityDiagnosticScope(selectedClientId);
        AccessToken token = inner.GetToken(requestContext, cancellationToken);
        Tokens.Add(token.Token);
        return token;
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        using var selectedIdentity = new AzureIdentityDiagnosticScope(selectedClientId);
        AccessToken token = await inner.GetTokenAsync(requestContext, cancellationToken);
        Tokens.Add(token.Token);
        return token;
    }
}
