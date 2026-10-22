using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Hexalith.EventStore.Server.Tests;

/// <summary>
/// Supplies one ephemeral JWT configuration to every in-process EventStore test host.
/// </summary>
internal static class AuthenticationTestEnvironment
{
    /// <summary>
    /// Gets the per-process symmetric signing key used by test token issuers.
    /// </summary>
    public static string SigningKey { get; private set; } = string.Empty;

    /// <summary>
    /// Initializes the process environment before any test host starts.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Environment.SetEnvironmentVariable("Authentication__JwtBearer__Authority", string.Empty);
        Environment.SetEnvironmentVariable("Authentication__JwtBearer__Issuer", "hexalith-dev");
        Environment.SetEnvironmentVariable("Authentication__JwtBearer__Audience", "hexalith-eventstore");
        Environment.SetEnvironmentVariable("Authentication__JwtBearer__SigningKey", SigningKey);
        Environment.SetEnvironmentVariable("Authentication__JwtBearer__RequireHttpsMetadata", "false");
    }
}
