using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Hexalith.EventStore.Admin.Server.Host.Tests;

/// <summary>
/// Supplies one ephemeral JWT configuration to every in-process Admin.Server test host.
/// </summary>
internal static class AuthenticationTestEnvironment
{
    /// <summary>
    /// Gets the per-process signing key used by host and token fixtures.
    /// </summary>
    public static string SigningKey { get; private set; } = string.Empty;

    /// <summary>
    /// Initializes the process environment before any test host starts.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        Environment.SetEnvironmentVariable("Authentication__JwtBearer__Authority", string.Empty);
        Environment.SetEnvironmentVariable("Authentication__JwtBearer__Issuer", "hexalith-dev");
        Environment.SetEnvironmentVariable("Authentication__JwtBearer__Audience", "hexalith-eventstore");
        SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        Environment.SetEnvironmentVariable("Authentication__JwtBearer__SigningKey", SigningKey);
        Environment.SetEnvironmentVariable("Authentication__JwtBearer__RequireHttpsMetadata", "false");
    }
}
