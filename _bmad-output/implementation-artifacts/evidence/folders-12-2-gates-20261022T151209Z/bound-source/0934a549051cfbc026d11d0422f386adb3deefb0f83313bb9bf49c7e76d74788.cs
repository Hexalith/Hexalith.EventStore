using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Shared constant-time verification of the Dapr application-channel token (AD-28).
/// </summary>
/// <remarks>
/// A Dapr sidecar started with <c>APP_API_TOKEN</c> presents that value in the <c>dapr-api-token</c> header on
/// every call it makes to its application. A valid token authenticates the sidecar channel only; it never
/// establishes the calling workload, a tenant, or an administrator.
/// </remarks>
public static class DaprAppChannelToken
{
    /// <summary>Gets the configuration key holding the application-channel token.</summary>
    public const string ConfigurationKey = "APP_API_TOKEN";

    /// <summary>Gets the header the Dapr sidecar uses to present the application-channel token.</summary>
    public const string HeaderName = "dapr-api-token";

    /// <summary>
    /// Verifies the channel token presented on a request against the host configuration.
    /// </summary>
    /// <param name="request">The inbound request.</param>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="environment">The host environment.</param>
    /// <returns>The verification status.</returns>
    public static DaprAppChannelTokenStatus Verify(
        HttpRequest request,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        return Verify(request, configuration[ConfigurationKey], environment.IsDevelopment());
    }

    /// <summary>
    /// Verifies the channel token presented on a request against an expected value.
    /// </summary>
    /// <param name="request">The inbound request.</param>
    /// <param name="expectedToken">The configured token, or <see langword="null"/> when none is configured.</param>
    /// <param name="isDevelopment">Whether the host runs in Development.</param>
    /// <returns>The verification status.</returns>
    public static DaprAppChannelTokenStatus Verify(HttpRequest request, string? expectedToken, bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            return isDevelopment ? DaprAppChannelTokenStatus.NotRequired : DaprAppChannelTokenStatus.Unconfigured;
        }

        StringValues header = request.Headers[HeaderName];
        if (header.Count > 1)
        {
            return DaprAppChannelTokenStatus.Duplicate;
        }

        if (header.Count == 0 || string.IsNullOrEmpty(header[0]))
        {
            return DaprAppChannelTokenStatus.Missing;
        }

        byte[] expected = Encoding.UTF8.GetBytes(expectedToken);
        byte[] actual = Encoding.UTF8.GetBytes(header[0]!);
        try
        {
            return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual)
                ? DaprAppChannelTokenStatus.Valid
                : DaprAppChannelTokenStatus.Invalid;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(actual);
        }
    }

    /// <summary>
    /// Gets whether a status admits the request through the channel check.
    /// </summary>
    /// <param name="status">The verification status.</param>
    /// <returns><see langword="true"/> for <see cref="DaprAppChannelTokenStatus.Valid"/> and the Development-only
    /// <see cref="DaprAppChannelTokenStatus.NotRequired"/>.</returns>
    public static bool IsAdmitted(DaprAppChannelTokenStatus status)
        => status is DaprAppChannelTokenStatus.Valid or DaprAppChannelTokenStatus.NotRequired;

    /// <summary>
    /// Maps a rejected status to its bounded, support-safe reason code.
    /// </summary>
    /// <param name="status">The verification status.</param>
    /// <returns>The reason code.</returns>
    public static string ToReasonCode(DaprAppChannelTokenStatus status) => status switch
    {
        DaprAppChannelTokenStatus.Unconfigured => WorkloadAuthenticationReasons.ChannelUnconfigured,
        DaprAppChannelTokenStatus.Missing => WorkloadAuthenticationReasons.ChannelTokenMissing,
        DaprAppChannelTokenStatus.Duplicate => WorkloadAuthenticationReasons.ChannelTokenDuplicate,
        DaprAppChannelTokenStatus.Invalid => WorkloadAuthenticationReasons.ChannelTokenInvalid,
        _ => WorkloadAuthenticationReasons.ChannelAdmitted,
    };
}
