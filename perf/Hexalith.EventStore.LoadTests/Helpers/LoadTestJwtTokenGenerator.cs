using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.LoadTests.Helpers;

/// <summary>
/// Generates synthetic dev-signed JWTs using the caller-supplied ephemeral signing key.
/// Mirrors tests/Hexalith.EventStore.IntegrationTests/Helpers/TestJwtTokenGenerator.cs
/// — kept inline here to keep the perf project free of test-project dependencies.
/// Only valid against an EventStore configured with the same ephemeral key.
/// </summary>
internal static class LoadTestJwtTokenGenerator {
    public const string Issuer = "hexalith-dev";
    public const string Audience = "hexalith-eventstore";

    private static readonly Lazy<SymmetricSecurityKey> s_securityKey = new(
        static () => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ResolveSigningKeyFromEnvironment())));
    private static readonly JwtSecurityTokenHandler s_handler = new();

    internal static string ResolveSigningKey(string? value) {
        if (string.IsNullOrWhiteSpace(value) || Encoding.UTF8.GetByteCount(value) < 32) {
            throw new InvalidOperationException(
                "LOAD_TEST_JWT_SIGNING_KEY must be nonblank, at least 32 UTF-8 bytes, and supplied from the same ephemeral source as the target EventStore host.");
        }

        return value;
    }

    private static string ResolveSigningKeyFromEnvironment()
        => ResolveSigningKey(Environment.GetEnvironmentVariable("LOAD_TEST_JWT_SIGNING_KEY"));

    public static string GenerateToken(
        string subject = "load-test-user",
        string[]? tenants = null,
        string[]? domains = null,
        string[]? permissions = null) {
        var claims = new List<Claim>
        {
            new("sub", subject),
        };

        if (tenants is not null) {
            claims.Add(new Claim("tenants", JsonSerializer.Serialize(tenants)));
        }

        if (domains is not null) {
            claims.Add(new Claim("domains", JsonSerializer.Serialize(domains)));
        }

        if (permissions is not null) {
            claims.Add(new Claim("permissions", JsonSerializer.Serialize(permissions)));
        }

        var descriptor = new SecurityTokenDescriptor {
            Subject = new ClaimsIdentity(claims),
            NotBefore = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddHours(1),
            IssuedAt = DateTime.UtcNow,
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(s_securityKey.Value, SecurityAlgorithms.HmacSha256Signature),
        };

        SecurityToken token = s_handler.CreateToken(descriptor);
        return s_handler.WriteToken(token);
    }
}
