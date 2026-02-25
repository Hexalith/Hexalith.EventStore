
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.IntegrationTests.Helpers;
/// <summary>
/// Generates JWT tokens for integration testing.
/// Uses a known symmetric key that must match the test host configuration.
/// </summary>
public static class TestJwtTokenGenerator {
    public const string SigningKey = "DevOnlySigningKey-AtLeast32Chars!";
    public const string Issuer = "hexalith-dev";
    public const string Audience = "hexalith-eventstore";

    private static readonly SymmetricSecurityKey s_securityKey = new(Encoding.UTF8.GetBytes(SigningKey));

    public static string GenerateToken(
        string subject = "test-user",
        string[]? tenants = null,
        string? tenantId = null,
        string[]? domains = null,
        string[]? permissions = null,
        DateTime? expires = null,
        string? issuer = null,
        string? audience = null) {
        var claims = new List<Claim>
        {
            new("sub", subject),
        };

        if (tenants is not null) {
            claims.Add(new Claim("tenants", JsonSerializer.Serialize(tenants)));
        }

        if (tenantId is not null) {
            claims.Add(new Claim("tenant_id", tenantId));
        }

        if (domains is not null) {
            claims.Add(new Claim("domains", JsonSerializer.Serialize(domains)));
        }

        if (permissions is not null) {
            claims.Add(new Claim("permissions", JsonSerializer.Serialize(permissions)));
        }

        DateTime expiresAt = expires ?? DateTime.UtcNow.AddHours(1);

        var tokenDescriptor = new SecurityTokenDescriptor {
            Subject = new ClaimsIdentity(claims),
            NotBefore = expiresAt < DateTime.UtcNow ? expiresAt.AddHours(-1) : DateTime.UtcNow,
            Expires = expiresAt,
            IssuedAt = expiresAt < DateTime.UtcNow ? expiresAt.AddHours(-2) : DateTime.UtcNow,
            Issuer = issuer ?? Issuer,
            Audience = audience ?? Audience,
            SigningCredentials = new SigningCredentials(s_securityKey, SecurityAlgorithms.HmacSha256Signature),
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
