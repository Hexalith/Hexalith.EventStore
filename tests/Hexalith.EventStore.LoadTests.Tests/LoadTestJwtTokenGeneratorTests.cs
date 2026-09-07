using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.LoadTests.Helpers;

using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.LoadTests.Tests;

public sealed class LoadTestJwtTokenGeneratorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    public void ResolveSigningKey_MissingBlankOrWeakValue_FailsSupportSafely(string? value)
    {
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => LoadTestJwtTokenGenerator.ResolveSigningKey(value));

        exception.Message.ShouldContain("LOAD_TEST_JWT_SIGNING_KEY");
        if (!string.IsNullOrWhiteSpace(value))
        {
            exception.Message.ShouldNotContain(value);
        }
    }

    [Fact]
    public void ResolveSigningKey_UsesUtf8ByteStrengthAndReturnsCallerValue()
    {
        string value = new('\u00e9', 16);

        string resolved = LoadTestJwtTokenGenerator.ResolveSigningKey(value);

        resolved.ShouldBe(value);
    }

    [Fact]
    public void GenerateToken_UsesConfiguredEphemeralSigningKeyAndExpectedClaims()
    {
        string? original = Environment.GetEnvironmentVariable("LOAD_TEST_JWT_SIGNING_KEY");
        string signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        try
        {
            Environment.SetEnvironmentVariable("LOAD_TEST_JWT_SIGNING_KEY", signingKey);

            string token = LoadTestJwtTokenGenerator.GenerateToken(
                subject: "load-user",
                tenants: ["tenant-a"],
                domains: ["counter"],
                permissions: ["command:submit"]);

            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            ClaimsPrincipal principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = LoadTestJwtTokenGenerator.Issuer,
                ValidateAudience = true,
                ValidAudience = LoadTestJwtTokenGenerator.Audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.Zero,
            }, out SecurityToken validatedToken);

            validatedToken.ShouldBeOfType<JwtSecurityToken>().Header.Alg.ShouldBe(SecurityAlgorithms.HmacSha256);
            principal.FindFirst("sub")!.Value.ShouldBe("load-user");
            principal.FindFirst("tenants")!.Value.ShouldContain("tenant-a");
            principal.FindFirst("domains")!.Value.ShouldContain("counter");
            principal.FindFirst("permissions")!.Value.ShouldContain("command:submit");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOAD_TEST_JWT_SIGNING_KEY", original);
        }
    }
}
