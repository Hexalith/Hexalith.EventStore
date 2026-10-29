using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>Supplies real credentials to existing host and redacted projection fixtures.</summary>
internal static class OperationsTestCredentials
{
    /// <summary>Gets the test receiver channel credential.</summary>
    internal const string Channel = "operations-host-test-channel";
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    /// <summary>Registers the fixture JWT profile and channel credential.</summary>
    /// <param name="builder">The isolated test host builder.</param>
    internal static void Configure(IWebHostBuilder builder) => builder.ConfigureAppConfiguration((context, configuration) =>
    {
        bool production = context.HostingEnvironment.IsProduction();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [DaprAppChannelToken.ConfigurationKey] = production ? "sidecar-secret" : Channel,
            ["Authentication:JwtBearer:Authority"] = production ? "https://operations-test.example.test" : null,
            ["Authentication:JwtBearer:Issuer"] = production ? "https://operations-test.example.test" : "operations-tests",
            ["Authentication:JwtBearer:Audience"] = "operations-tests",
            ["Authentication:JwtBearer:SigningKey"] = production ? null : SigningKey,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = production ? SecurityAlgorithms.RsaSha256 : SecurityAlgorithms.HmacSha256,
        });
    });

    /// <summary>Creates a short-lived signed Admin credential for this test profile.</summary>
    /// <returns>The fixture bearer token.</returns>
    internal static string HumanToken() => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "operations-tests", Audience = "operations-tests", Expires = DateTime.UtcNow.AddMinutes(5),
        Claims = new Dictionary<string, object> { ["sub"] = "validated-operator", ["global_admin"] = true },
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
    });
}
