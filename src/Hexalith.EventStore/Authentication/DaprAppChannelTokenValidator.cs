using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Authentication;

/// <summary>Confirms that an internal HTTP call came through the configured Dapr app channel.</summary>
public sealed class DaprAppChannelTokenValidator(
    IHostEnvironment environment,
    IConfiguration configuration)
{
    /// <summary>Gets the configuration key for the application-channel token.</summary>
    public const string ConfigurationKey = "APP_API_TOKEN";

    /// <summary>Gets the Dapr application-channel token header name.</summary>
    public const string HeaderName = "dapr-api-token";

    /// <summary>Gets whether a non-Development host has the required app-channel secret.</summary>
    public bool IsConfigured => environment.IsDevelopment()
        || !string.IsNullOrWhiteSpace(configuration[ConfigurationKey]);

    /// <summary>Validates the app-channel token outside local Development.</summary>
    public bool IsValid(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (environment.IsDevelopment())
        {
            return true;
        }

        string? expectedToken = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            return false;
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
