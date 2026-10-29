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
namespace Hexalith.EventStore.Admin.Server.Host.Tests;

/// <summary>Verifies the Admin host names resolved policies and protects its documentation assets.</summary>
public sealed class HttpHostSecurityTests
{
    /// <summary>Verifies admin Inventory Resolves Every Named Policy And Scheme.</summary>
    [Fact]
    public async Task AdminInventoryResolvesEveryNamedPolicyAndScheme()
    {
        await using var factory = new HostBootstrapTests.AdminServerHostFactory();
        using HttpClient client = factory.CreateClient();
        var endpoints = factory.Services.GetServices<EndpointDataSource>().SelectMany(static source => source.Endpoints).ToArray();
        endpoints.Length.ShouldBeGreaterThan(20);
        (await EventStoreHttpEndpointInventory.ValidateAsync(endpoints, factory.Services).ConfigureAwait(true)).ShouldBeEmpty();
        using HttpResponseMessage alive = await client.GetAsync("/alive", TestContext.Current.CancellationToken).ConfigureAwait(true);
        alive.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Verifies admin Documentation Requires Human Authentication.</summary>
    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/swagger-ui.css")]
    public async Task AdminDocumentationRequiresHumanAuthentication(string path)
    {
        await using var factory = new HostBootstrapTests.AdminServerHostFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using HttpResponseMessage denied = await client.GetAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);
        denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "hexalith-dev", Audience = "hexalith-eventstore", Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object> { ["sub"] = "operator" },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthenticationTestEnvironment.SigningKey)), SecurityAlgorithms.HmacSha256),
        }));
        using HttpResponseMessage admitted = await client.GetAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);
        admitted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
