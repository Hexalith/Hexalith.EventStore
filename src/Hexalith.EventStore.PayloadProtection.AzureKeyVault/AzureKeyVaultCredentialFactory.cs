// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 11 and 15.
using Azure.Core;
using Azure.Identity;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Creates exactly one managed identity in every environment, with no credential fallback or application-configured token endpoint.</summary>
internal sealed class AzureKeyVaultCredentialFactory(TokenCredential inner) : TokenCredential
{
    /// <summary>Constructs the deterministic system/user-assigned identity with SDK retries and unsafe diagnostics disabled.</summary>
    internal static TokenCredential Create(AzureKeyVaultPayloadProtectionOptions options)
    {
        _ = AzureKeyVaultOptionsValidator.Validate(options);
        var id = options.ManagedIdentityClientId is null ? ManagedIdentityId.SystemAssigned : ManagedIdentityId.FromUserAssignedClientId(options.ManagedIdentityClientId);
        var credentialOptions = new ManagedIdentityCredentialOptions(id);
        Configure(credentialOptions, new AzureKeyVaultSafeTransport(options, identity: true));
        credentialOptions.Diagnostics.IsAccountIdentifierLoggingEnabled = false;
        credentialOptions.IsUnsafeSupportLoggingEnabled = false;
        return Bound(new ManagedIdentityCredential(credentialOptions));
    }

    /// <summary>Suppresses the SDK cache's background refresh without extending token expiry or introducing another credential.</summary>
    internal static TokenCredential Bound(TokenCredential credential) => new AzureKeyVaultCredentialFactory(credential);
    /// <inheritdoc/>
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        using var acquisition = new AzureKeyVaultTokenAcquisition(cancellationToken);
        AccessToken token;
        try
        {
            token = inner.GetToken(requestContext, acquisition.Token);
        }
        catch when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch when (acquisition.Failure is not null)
        {
            throw new AuthenticationFailedException("Managed identity acquisition failed.", new AzureKeyVaultTransportException(acquisition.Failure.Value));
        }

        return token.BindingCertificate is null ? new AccessToken(token.Token, token.ExpiresOn, token.ExpiresOn, token.TokenType) : new AccessToken(token.Token, token.ExpiresOn, token.ExpiresOn, token.TokenType, token.BindingCertificate);
    }

    /// <inheritdoc/>
    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        using var acquisition = new AzureKeyVaultTokenAcquisition(cancellationToken);
        AccessToken token;
        try
        {
            token = await inner.GetTokenAsync(requestContext, acquisition.Token).ConfigureAwait(false);
        }
        catch when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch when (acquisition.Failure is not null)
        {
            throw new AuthenticationFailedException("Managed identity acquisition failed.", new AzureKeyVaultTransportException(acquisition.Failure.Value));
        }

        return token.BindingCertificate is null ? new AccessToken(token.Token, token.ExpiresOn, token.ExpiresOn, token.TokenType) : new AccessToken(token.Token, token.ExpiresOn, token.ExpiresOn, token.TokenType, token.BindingCertificate);
    }

    /// <summary>Applies the explicit shared retry/diagnostic constraints to every SDK client.</summary>
    internal static void Configure(ClientOptions options, Azure.Core.Pipeline.HttpPipelineTransport transport)
    {
        options.Transport = transport;
        options.Retry.MaxRetries = 0;
        options.RetryPolicy = null;
        options.Diagnostics.IsLoggingContentEnabled = false;
        options.Diagnostics.IsLoggingEnabled = false;
        options.Diagnostics.IsDistributedTracingEnabled = false;
        options.Diagnostics.IsTelemetryEnabled = false;
    }
}
