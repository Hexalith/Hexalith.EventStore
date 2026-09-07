extern alias eventstore;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.EventStore.Server.Tests.Authentication;

/// <summary>
/// Verifies the EventStore HTTP pipeline against the Production authority-mode token matrix.
/// </summary>
public sealed class ProductionAuthorityAuthenticationTests
{
    private const string AdditionalAudience = "hexalith-eventstore-secondary";
    private const string Audience = "hexalith-eventstore";
    private const string AuthorityIssuer = "https://identity.example.test/realms/hexalith";
    private const string ProtectedRoute = "/api/v1/commands/status/test-message";

    /// <summary>
    /// Proves that the real EventStore host accepts an additional configured audience and rejects
    /// every invalid authority-mode token dimension before protected controller work.
    /// </summary>
    [Fact]
    public async Task EventStoreHttpPipeline_InProductionAuthorityMode_EnforcesEveryTokenValidationDimension()
    {
        using RSA rsa = RSA.Create(2048);
        using RSA wrongRsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = Guid.NewGuid().ToString("N") };
        var wrongSigningKey = new RsaSecurityKey(wrongRsa) { KeyId = Guid.NewGuid().ToString("N") };

        await using WebApplicationFactory<EventStoreProgram> factory = new WebApplicationFactory<EventStoreProgram>()
            .WithWebHostBuilder(builder =>
            {
                _ = builder.UseEnvironment(Environments.Production);
                _ = builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Authentication:JwtBearer:Authority"] = AuthorityIssuer,
                        ["Authentication:JwtBearer:Issuer"] = AuthorityIssuer,
                        ["Authentication:JwtBearer:Audience"] = Audience,
                        ["Authentication:JwtBearer:ValidAudiences:0"] = AdditionalAudience,
                        ["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.RsaSha256,
                        ["Authentication:JwtBearer:SigningKey"] = null,
                        ["Authentication:JwtBearer:RequireHttpsMetadata"] = "true",
                    }));
                builder.ConfigureTestServices(WebApplicationFactoryServiceOverrides.RemoveAdminOperationalIndexHostedService);
            });
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        ConfigureStaticAuthority(factory.Services, signingKey);

        using (var validRequest = CreateRequest(CreateRsaToken(signingKey, AuthorityIssuer, AdditionalAudience)))
        using (HttpResponseMessage validResponse = await client.SendAsync(
            validRequest,
            TestContext.Current.CancellationToken))
        {
            validResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        string symmetricKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        (string Scenario, string Token)[] invalidTokens =
        [
            ("unsigned", CreateRsaToken(signingKey, AuthorityIssuer, Audience, signed: false)),
            ("missing expiry", CreateRsaToken(signingKey, AuthorityIssuer, Audience, includeExpiry: false)),
            ("expired", CreateRsaToken(signingKey, AuthorityIssuer, Audience, expires: DateTime.UtcNow.AddMinutes(-10))),
            ("wrong issuer", CreateRsaToken(signingKey, "https://unexpected.example.test", Audience)),
            ("wrong audience", CreateRsaToken(signingKey, AuthorityIssuer, "unexpected-audience")),
            ("wrong signing key", CreateRsaToken(wrongSigningKey, AuthorityIssuer, Audience)),
            ("wrong algorithm family", CreateSymmetricToken(symmetricKey)),
            ("non-allowlisted algorithm", CreateRsaToken(
                signingKey,
                AuthorityIssuer,
                Audience,
                SecurityAlgorithms.RsaSha384)),
            ("missing algorithm", CreateTokenWithoutAlgorithm()),
        ];

        foreach ((string scenario, string token) in invalidTokens)
        {
            using HttpRequestMessage request = CreateRequest(token);
            using HttpResponseMessage response = await client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, scenario);
        }
    }

    private static void ConfigureStaticAuthority(IServiceProvider services, SecurityKey signingKey)
    {
        var configuration = new OpenIdConnectConfiguration { Issuer = AuthorityIssuer };
        configuration.SigningKeys.Add(signingKey);
        services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
    }

    private static HttpRequestMessage CreateRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ProtectedRoute);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string CreateRsaToken(
        SecurityKey signingKey,
        string issuer,
        string audience,
        string algorithm = SecurityAlgorithms.RsaSha256,
        DateTime? expires = null,
        bool signed = true,
        bool includeExpiry = true)
    {
        DateTime expiresAt = expires ?? DateTime.UtcNow.AddMinutes(30);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            [new Claim("sub", "authority-mode-user")],
            expiresAt < DateTime.UtcNow ? expiresAt.AddMinutes(-30) : DateTime.UtcNow.AddMinutes(-1),
            includeExpiry ? expiresAt : null,
            signed ? new SigningCredentials(signingKey, algorithm) : null);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateSymmetricToken(string signingKey)
        => CreateRsaToken(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            AuthorityIssuer,
            Audience,
            SecurityAlgorithms.HmacSha256Signature);

    private static string CreateTokenWithoutAlgorithm()
    {
        string header = Base64UrlEncoder.Encode("{\"typ\":\"JWT\"}");
        string payload = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new
        {
            iss = AuthorityIssuer,
            aud = Audience,
            sub = "authority-mode-user",
            exp = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds(),
        }));
        return $"{header}.{payload}.";
    }
}
