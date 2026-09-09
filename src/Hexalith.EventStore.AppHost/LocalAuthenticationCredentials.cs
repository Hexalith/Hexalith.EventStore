using System.Security.Cryptography;

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
    private static readonly AsyncLocal<LocalAuthenticationTestInvocation?> ActiveTestInvocation = new();

    /// <summary>
    /// Creates a fresh credential set, except when an in-process test caller has activated a
    /// caller-held, one-use invocation capability. Normal AppHost configuration cannot address or
    /// manufacture that capability.
    /// </summary>
    /// <returns>A single credential set shared by every local authentication consumer.</returns>
    public static LocalAuthenticationCredentials Create()
    {
        LocalAuthenticationTestInvocation? invocation = ActiveTestInvocation.Value;
        if (invocation is null)
        {
            return Generate();
        }

        ActiveTestInvocation.Value = null;
        return invocation.Consume();
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

        return new LocalAuthenticationTestInvocation(generated);
    }

    /// <summary>
    /// Activates the exact caller-held capability for the current asynchronous invocation flow.
    /// </summary>
    /// <param name="invocation">The unconsumed test invocation capability.</param>
    internal static void ActivateTestInvocation(LocalAuthenticationTestInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        if (ActiveTestInvocation.Value is not null)
        {
            throw new InvalidOperationException(
                "A local authentication test invocation is already active for this asynchronous flow.");
        }

        ActiveTestInvocation.Value = invocation;
    }

    /// <summary>
    /// Clears an unconsumed test capability from the current asynchronous invocation flow.
    /// </summary>
    /// <param name="invocation">The capability that owns the activation.</param>
    internal static void DeactivateTestInvocation(LocalAuthenticationTestInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        if (ReferenceEquals(ActiveTestInvocation.Value, invocation))
        {
            ActiveTestInvocation.Value = null;
        }
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

}
