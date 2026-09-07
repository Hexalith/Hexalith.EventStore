using System.Security.Cryptography;

using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.AppHost;

/// <summary>
/// Holds one cryptographically generated local authentication identity set for an AppHost run.
/// </summary>
internal sealed record LocalAuthenticationCredentials(
    string SigningKey,
    string AdminUserId,
    string AdminUsername,
    string AdminPassword,
    string TenantAUserId,
    string TenantAPassword,
    string TenantBUserId,
    string TenantBPassword,
    string ReadOnlyUserId,
    string ReadOnlyPassword,
    string NoTenantUserId,
    string NoTenantPassword)
{
    /// <summary>
    /// Creates credentials from explicit runtime configuration or cryptographically random values.
    /// </summary>
    /// <param name="configuration">The AppHost configuration.</param>
    /// <returns>A single credential set shared by every local authentication consumer.</returns>
    public static LocalAuthenticationCredentials Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new LocalAuthenticationCredentials(
            Resolve("SigningKey", 48),
            ResolveUserId("AdminUserId"),
            ResolveIdentifier("AdminUsername"),
            Resolve("AdminPassword", 24),
            ResolveUserId("TenantAUserId"),
            Resolve("TenantAPassword", 24),
            ResolveUserId("TenantBUserId"),
            Resolve("TenantBPassword", 24),
            ResolveUserId("ReadOnlyUserId"),
            Resolve("ReadOnlyPassword", 24),
            ResolveUserId("NoTenantUserId"),
            Resolve("NoTenantPassword", 24));

        string Resolve(string name, int byteCount)
        {
            string? configured = configuration[$"LocalAuthentication:{name}"];
            return !string.IsNullOrWhiteSpace(configured) ? configured : Generate(byteCount);
        }

        string ResolveIdentifier(string name)
        {
            string? configured = configuration[$"LocalAuthentication:{name}"];
            return !string.IsNullOrWhiteSpace(configured)
                ? configured
                : $"local-{Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant()}";
        }

        string ResolveUserId(string name)
        {
            string? configured = configuration[$"LocalAuthentication:{name}"];
            return !string.IsNullOrWhiteSpace(configured)
                ? configured
                : Guid.NewGuid().ToString("D");
        }
    }

    private static string Generate(int byteCount)
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
