using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Authentication;

/// <summary>Confirms that an internal HTTP call came through the configured Dapr app channel.</summary>
public sealed class DaprAppChannelTokenValidator(
    IHostEnvironment environment,
    IConfiguration configuration,
    IOptionsMonitor<DaprInternalAuthenticationOptions>? internalOptions = null)
{
    /// <summary>Gets the configuration key for the application-channel token.</summary>
    public const string ConfigurationKey = "APP_API_TOKEN";

    /// <summary>Gets the Dapr application-channel token header name.</summary>
    public const string HeaderName = "dapr-api-token";

    /// <summary>
    /// Gets whether the host is ready for internal callers: Development, no allow-listed internal
    /// callers, or a configured app-channel secret. Without the options the secret is required.
    /// </summary>
    public bool IsConfigured => environment.IsDevelopment()
        || (internalOptions is not null
            && internalOptions.Get(DaprInternalAuthenticationOptions.SchemeName).AllowedCallers.Count == 0)
        || !string.IsNullOrWhiteSpace(configuration[ConfigurationKey]);

    /// <summary>
    /// Validates the app-channel token. A configured token is always compared; only Development
    /// without a configured token admits the call without one.
    /// </summary>
    public bool IsValid(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? expectedToken = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            return environment.IsDevelopment();
        }

        Microsoft.Extensions.Primitives.StringValues header = request.Headers[HeaderName];
        if (header.Count != 1 || string.IsNullOrEmpty(header[0]))
        {
            return false;
        }

        byte[] expected = Encoding.UTF8.GetBytes(expectedToken);
        byte[] actual = Encoding.UTF8.GetBytes(header[0]!);
        try
        {
            return expected.Length == actual.Length
                && CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(actual);
        }
    }
}
