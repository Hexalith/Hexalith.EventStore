using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.LoadTests.Helpers;

/// <summary>
/// Generates synthetic dev-signed JWTs that match the EventStore test signing key.
/// Mirrors tests/Hexalith.EventStore.IntegrationTests/Helpers/TestJwtTokenGenerator.cs
/// — kept inline here to keep the perf project free of test-project dependencies.
/// Only valid against an EventStore configured with the dev signing key.
/// </summary>
internal static class LoadTestJwtTokenGenerator {
    public const string SigningKey = "DevOnlySigningKey-AtLeast32Chars!";
    public const string Issuer = "hexalith-dev";
    public const string Audience = "hexalith-eventstore";

    private static readonly SymmetricSecurityKey s_securityKey = new(Encoding.UTF8.GetBytes(SigningKey));
    private static readonly JwtSecurityTokenHandler s_handler = new();

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
            SigningCredentials = new SigningCredentials(s_securityKey, SecurityAlgorithms.HmacSha256Signature),
        };

        SecurityToken token = s_handler.CreateToken(descriptor);
        return s_handler.WriteToken(token);
    }
}
