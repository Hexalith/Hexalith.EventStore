using System.Text;

using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.Server.Tests.TestUtilities;

/// <summary>
/// Mints workload assertions for in-process hosts that run the symmetric Development JWT contract.
/// </summary>
internal static class WorkloadAssertionTestTokens
{
    /// <summary>Gets the issuer configured by <see cref="AuthenticationTestEnvironment"/>.</summary>
    public const string Issuer = "hexalith-dev";

    /// <summary>
    /// Creates a signed workload assertion.
    /// </summary>
    /// <param name="audience">The receiving audience.</param>
    /// <param name="caller">The <c>azp</c> caller, or <see langword="null"/> to omit it.</param>
    /// <param name="operations">The granted operations.</param>
    /// <param name="lifetime">The lifetime from issue to expiry.</param>
    /// <param name="issuedAt">The issue time; defaults to now.</param>
    /// <param name="bindings">Optional resource bindings.</param>
    /// <param name="signingKey">The signing key; defaults to the test-process key.</param>
    /// <param name="issuer">The issuer; defaults to <see cref="Issuer"/>.</param>
    /// <returns>The serialized assertion.</returns>
    public static string Create(
        string audience,
        string? caller,
        IReadOnlyCollection<string> operations,
        TimeSpan? lifetime = null,
        DateTime? issuedAt = null,
        IReadOnlyDictionary<string, string>? bindings = null,
        string? signingKey = null,
        string? issuer = null)
    {
        DateTime issued = issuedAt ?? DateTime.UtcNow;
        var claims = new Dictionary<string, object>(StringComparer.Ordinal);
        if (caller is not null)
        {
            claims[EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = caller;
        }

        if (operations.Count > 0)
        {
            claims[EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = operations.Count == 1
                ? operations.Single()
                : operations.ToArray();
        }

        foreach (KeyValuePair<string, string> binding in bindings ?? new Dictionary<string, string>())
        {
            claims[binding.Key] = binding.Value;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer ?? Issuer,
            Audience = audience,
            IssuedAt = issued,
            NotBefore = issued,
            Expires = issued.Add(lifetime ?? TimeSpan.FromMinutes(2)),
            Claims = claims,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? AuthenticationTestEnvironment.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }
}
