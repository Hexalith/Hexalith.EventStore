extern alias eventstore;

using System.Net;

using Hexalith.EventStore.IntegrationTests.Helpers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using Shouldly;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.EventStore.IntegrationTests.EventStore;

public sealed class ApplicationRuntimeProofEndpointTests {
    private const string Endpoint = "/_test/runtime-proof/shutdown";
    private const string Header = "X-Hexalith-Runtime-Proof-Token";
    private static readonly string Token = Guid.NewGuid().ToString("N");

    [Fact]
    public async Task ShutdownEndpointWithoutExplicitTokenIsNotMappedAsync() {
        using var factory = new JwtAuthenticatedWebApplicationFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(Endpoint, content: null).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ShutdownEndpointWithWrongTokenIsConcealedAsync() {
        using WebApplicationFactory<EventStoreProgram> factory = CreateFactory(Environments.Development);
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add(Header, Guid.NewGuid().ToString("N"));

        using HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ShutdownEndpointIsNeverMappedInProductionAsync() {
        using WebApplicationFactory<EventStoreProgram> factory = CreateFactory(Environments.Production);
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add(Header, Token);

        using HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static WebApplicationFactory<EventStoreProgram> CreateFactory(string environment) {
        var baseFactory = new JwtAuthenticatedWebApplicationFactory();
        return baseFactory.WithWebHostBuilder(builder => {
            _ = builder.UseEnvironment(environment);
            var settings = new Dictionary<string, string?> {
                ["EventStore:RuntimeProof:ShutdownToken"] = Token,
                ["Authentication:JwtBearer:AllowInsecureSymmetricKey"] = "true",
            };
            if (environment == Environments.Production) {
                settings["Authentication:JwtBearer:Authority"] = "https://identity.example.invalid/realms/hexalith";
                settings["Authentication:JwtBearer:Issuer"] = "https://identity.example.invalid/realms/hexalith";
                settings["Authentication:JwtBearer:SigningKey"] = string.Empty;
                settings["Authentication:JwtBearer:AllowedAlgorithms:0"] = "RS256";
                settings["Authentication:JwtBearer:RequireHttpsMetadata"] = "true";
                settings["Authentication:JwtBearer:AllowInsecureSymmetricKey"] = "false";
            }

            _ = builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(settings));
        });
    }
}
