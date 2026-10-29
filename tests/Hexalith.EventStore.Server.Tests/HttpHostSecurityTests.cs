extern alias eventstore;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Hexalith.EventStore.Server.Tests.OpenApi;

namespace Hexalith.EventStore.Server.Tests;

/// <summary>Verifies the gateway's effective endpoint inventory and protected documentation middleware.</summary>
public sealed class HttpHostSecurityTests
{
    /// <summary>Verifies gateway Inventory Resolves Every Named Policy And Scheme.</summary>
    [Fact]
    public async Task GatewayInventoryResolvesEveryNamedPolicyAndScheme()
    {
        await using var factory = new OpenApiWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        var endpoints = factory.Services.GetServices<EndpointDataSource>().SelectMany(static source => source.Endpoints).ToArray();
        endpoints.Length.ShouldBeGreaterThan(20);
        (await EventStoreHttpEndpointInventory.ValidateAsync(endpoints, factory.Services).ConfigureAwait(true)).ShouldBeEmpty();
    }

    /// <summary>Verifies documentation And Fallback Require Human Authentication.</summary>
    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/swagger-ui.css")]
    [InlineData("/problems")]
    [InlineData("/api/v2/example")]
    public async Task DocumentationAndFallbackRequireHumanAuthentication(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        await using var factory = new OpenApiWebApplicationFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = null;
        using HttpResponseMessage denied = await client.GetAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);
        denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "hexalith-dev", Audience = "hexalith-eventstore", Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object> { ["sub"] = "operator" },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthenticationTestEnvironment.SigningKey)), SecurityAlgorithms.HmacSha256),
        }));
        using HttpResponseMessage admitted = await client.GetAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);
        admitted.StatusCode.ShouldBe(path.StartsWith("/api/", StringComparison.Ordinal) ? HttpStatusCode.BadRequest : HttpStatusCode.OK);
    }

    /// <summary>Verifies framework Routes Deny Direct Untokened Requests.</summary>
    [Theory]
    [InlineData("/dapr/subscribe")]
    [InlineData("/dapr/config")]
    [InlineData("/healthz")]
    public async Task FrameworkRoutesDenyDirectUntokenedRequests(string path)
    {
        await using var factory = new OpenApiWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;
        using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
