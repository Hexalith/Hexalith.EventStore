using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Authentication;

/// <summary>Confirms that an internal HTTP call came through the configured Dapr app channel (AD-28).</summary>
/// <remarks>The token authenticates the sidecar channel only; it never identifies the calling workload.</remarks>
public sealed class DaprAppChannelTokenValidator(
    IHostEnvironment environment,
    IConfiguration configuration,
    IOptionsMonitor<DaprInternalAuthenticationOptions>? internalOptions = null)
{
    /// <summary>Gets the configuration key for the application-channel token.</summary>
    public const string ConfigurationKey = DaprAppChannelToken.ConfigurationKey;

    /// <summary>Gets the Dapr application-channel token header name.</summary>
    public const string HeaderName = DaprAppChannelToken.HeaderName;

    /// <summary>
    /// Gets whether the host is ready for internal callers: Development, no allow-listed internal
    /// callers, or a configured app-channel secret. Without the options the secret is required.
    /// </summary>
    public bool IsConfigured => environment.IsDevelopment()
        || (internalOptions is not null
            && internalOptions.Get(DaprInternalAuthenticationOptions.SchemeName).AllowedCallers.Count == 0)
        || !string.IsNullOrWhiteSpace(configuration[ConfigurationKey]);

    /// <summary>
    /// Validates the app-channel token with the shared constant-time verifier. A configured token is always
    /// compared; only Development without a configured token admits the call without one.
    /// </summary>
    /// <param name="request">The inbound request.</param>
    /// <returns><see langword="true"/> when the channel is admitted.</returns>
    public bool IsValid(HttpRequest request)
        => DaprAppChannelToken.IsAdmitted(DaprAppChannelToken.Verify(request, configuration, environment));
}
