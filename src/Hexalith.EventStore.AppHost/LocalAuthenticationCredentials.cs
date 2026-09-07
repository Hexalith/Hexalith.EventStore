using System.Collections.Concurrent;
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
    private const string TestInvocationConfigurationKey = "LocalAuthentication:TestInjection:InvocationId";
    private static readonly ConcurrentDictionary<Guid, LocalAuthenticationCredentials> s_testInvocations = new();

    /// <summary>
    /// Creates a fresh credential set, except when an in-process test caller has registered the exact
    /// one-use invocation capability named by configuration. Credential values themselves are never
    /// read from configuration.
    /// </summary>
    /// <param name="configuration">The AppHost configuration.</param>
    /// <returns>A single credential set shared by every local authentication consumer.</returns>
    public static LocalAuthenticationCredentials Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? configuredInvocation = configuration[TestInvocationConfigurationKey];
        if (string.IsNullOrWhiteSpace(configuredInvocation))
        {
            return Generate();
        }

        if (!Guid.TryParseExact(configuredInvocation, "D", out Guid invocationId)
            || !s_testInvocations.TryRemove(invocationId, out LocalAuthenticationCredentials? injected))
        {
            throw new InvalidOperationException(
                $"{TestInvocationConfigurationKey} does not identify an active caller-held test invocation.");
        }

        return injected;
    }

    /// <summary>
    /// Registers a one-use, in-process test invocation. Possession of the returned lease is the
    /// capability; configuration alone cannot create or populate a registration.
    /// </summary>
    internal static LocalAuthenticationTestInvocation RegisterTestInvocation(
        string signingKey,
        string? adminUserId = null,
        string? adminUsername = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);

        string resolvedAdminUsername = string.IsNullOrWhiteSpace(adminUsername)
            ? $"local-{Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant()}"
            : adminUsername.Trim();
        if (resolvedAdminUsername is "tenant-a-user" or "tenant-b-user" or "readonly-user" or "no-tenant-user")
        {
            throw new ArgumentException(
                "The test administrator username must not collide with a fixed local realm identity.",
                nameof(adminUsername));
        }

        LocalAuthenticationCredentials generated = Generate() with
        {
            SigningKey = signingKey,
            AdminUserId = string.IsNullOrWhiteSpace(adminUserId) ? Guid.NewGuid().ToString("D") : adminUserId,
            AdminUsername = resolvedAdminUsername,
        };

        Guid invocationId;
        do
        {
            invocationId = Guid.NewGuid();
        }
        while (!s_testInvocations.TryAdd(invocationId, generated));

        return new LocalAuthenticationTestInvocation(invocationId, generated, RemoveTestInvocation);
    }

    private static LocalAuthenticationCredentials Generate()
        => new(
            GenerateSecret(48),
            Guid.NewGuid().ToString("D"),
            $"local-{Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant()}",
            GenerateSecret(24),
            Guid.NewGuid().ToString("D"),
            GenerateSecret(24),
            Guid.NewGuid().ToString("D"),
            GenerateSecret(24),
            Guid.NewGuid().ToString("D"),
            GenerateSecret(24),
            Guid.NewGuid().ToString("D"),
            GenerateSecret(24));

    private static string GenerateSecret(int byteCount)
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static void RemoveTestInvocation(Guid invocationId)
        => _ = s_testInvocations.TryRemove(invocationId, out _);
}

/// <summary>
/// Caller-held lifetime for one explicitly registered AppHost test invocation.
/// </summary>
internal sealed class LocalAuthenticationTestInvocation : IDisposable
{
    private readonly Action<Guid> _remove;
    private bool _disposed;

    internal LocalAuthenticationTestInvocation(
        Guid invocationId,
        LocalAuthenticationCredentials credentials,
        Action<Guid> remove)
    {
        InvocationId = invocationId;
        Credentials = credentials;
        _remove = remove;
    }

    /// <summary>Gets the opaque invocation identifier that may be placed in test configuration.</summary>
    public Guid InvocationId { get; }

    /// <summary>Gets the registered values so the test issuer can use the same ephemeral key.</summary>
    public LocalAuthenticationCredentials Credentials { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _remove(InvocationId);
        _disposed = true;
    }
}
