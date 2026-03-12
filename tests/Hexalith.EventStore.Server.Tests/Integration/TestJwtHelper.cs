
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.Server.Tests.Integration;

/// <summary>
/// Generates JWT tokens for Tier 2 integration tests.
/// Uses the same signing key as appsettings.Development.json.
/// </summary>
internal static class TestJwtHelper {
    private const string SigningKey = "DevOnlySigningKey-AtLeast32Chars!";
    private const string Issuer = "hexalith-dev";
    private const string Audience = "hexalith-eventstore";

    private static readonly SymmetricSecurityKey s_securityKey = new(Encoding.UTF8.GetBytes(SigningKey));

    public static string GenerateToken(
        string subject = "test-user",
        string[]? tenants = null,
        string[]? domains = null,
        string[]? permissions = null) {
        var claims = new List<Claim> { new("sub", subject) };

        if (tenants is not null) {
            claims.Add(new Claim("tenants", JsonSerializer.Serialize(tenants)));
        }

        if (domains is not null) {
            claims.Add(new Claim("domains", JsonSerializer.Serialize(domains)));
        }

        if (permissions is not null) {
            claims.Add(new Claim("permissions", JsonSerializer.Serialize(permissions)));
        }

        var tokenDescriptor = new SecurityTokenDescriptor {
            Subject = new ClaimsIdentity(claims),
            NotBefore = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddHours(1),
            IssuedAt = DateTime.UtcNow,
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(s_securityKey, SecurityAlgorithms.HmacSha256Signature),
        };

        var handler = new JwtSecurityTokenHandler();
        SecurityToken token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }
}
