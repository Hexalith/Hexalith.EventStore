using System.Security.Claims;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

public sealed partial class DomainServiceWorkloadAssertionHandlerTests
{
    [Theory]
    [InlineData("projects", "ProjectUser")]
    [InlineData("folders", "FolderReader")]
    public void DomainRoleCaptureDoesNotPromoteForeignNamespacesOrScopes(string domain, string expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { [$"Authentication:QueryAuthority:RequiredQueries:{domain}:0"] = "BoundRead" }).Build();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("projects:role", "ProjectUser"),
            new Claim("folders:role", "FolderReader"), new Claim("foreign:role", "Administrator"),
            new Claim("scope", "projects:inspect_descriptive_metadata")], "validated-human", "sub", "foreign:role"));
        QueryCallerAuthority authority = GatewayQueryCallerAuthority.Capture(principal, configuration, domain, "BoundRead").ShouldNotBeNull();
        authority.Roles.ShouldBe([expected]); authority.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task OidcLegacyQueriesRemainUnboundWhileExplicitAuthorityQueriesFailClosed()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Authority"] = ScopedWorkloadTokenEndpoint.Authority,
            ["Authentication:JwtBearer:Issuer"] = ScopedWorkloadTokenEndpoint.Authority,
            ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "RS256",
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "true",
            ["Authentication:WorkloadIssuer:ClientId"] = "eventstore", ["Authentication:WorkloadIssuer:ClientSecret"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            ["Authentication:WorkloadIssuer:TokenEndpoint"] = ScopedWorkloadTokenEndpoint.Authority + "/protocol/openid-connect/token",
            ["Authentication:QueryAuthority:RequiredQueries:projects:0"] = "BoundRead",
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "actor"), new Claim("scope", "inspect_metadata"),
            new Claim("projects:role", "ProjectUser"), new Claim("folders:role", "ForeignAdministrator"), new Claim("eventstore:permission", "read_project")], "validated-human"));
        GatewayQueryCallerAuthority.Capture(principal, configuration, "projects", "LegacyRead").ShouldBeNull();
        QueryCallerAuthority authority = GatewayQueryCallerAuthority.Capture(principal, configuration, "projects", "BoundRead").ShouldNotBeNull();
        authority.Roles.ShouldBe(["ProjectUser"]);
        authority.Permissions.ShouldBe(["read_project"]); // OAuth scope is never an inspection grant.
        var capture = new CapturingHandler(); var endpoint = new ScopedWorkloadTokenEndpoint();
        IServiceCollection services = CreateHostServices(settings, Environments.Production);
        services.AddEventStoreServer(configuration);
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => capture));
        services.AddHttpClient(JwtWorkloadAssertionIssuer.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => endpoint);
        await using ServiceProvider provider = services.BuildServiceProvider();
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient();
        using var legacy = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:3501/v1.0/invoke/projects/method/query") { Content = new StringContent("{}") };
        using HttpResponseMessage first = await client.SendAsync(legacy, TestContext.Current.CancellationToken);
        ScopedWorkloadTokenEndpoint.AssertExactlyScoped(capture.Assertions.ShouldHaveSingleItem(), "projects", EventStoreWorkloadOperations.DomainServiceQuery);
        using var bound = new HttpRequestMessage(HttpMethod.Post, legacy.RequestUri) { Content = new StringContent("{}") };
        bound.Options.Set(QueryCallerAuthorityBinding.RequestBindings, QueryCallerAuthorityBinding.Bind(new QueryEnvelope("tenant", "projects", "project", "BoundRead", [], "corr", "actor") { OriginalActorId = "actor", CallerAuthority = authority }));
        using HttpResponseMessage second = await client.SendAsync(bound, TestContext.Current.CancellationToken);
        capture.Assertions.ShouldBeEmpty(); endpoint.Scopes.Count.ShouldBe(1);
    }
}
