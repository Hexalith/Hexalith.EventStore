using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Verifies supported gateway routing and exact workload grants through the real SDK issuer.</summary>
public sealed class EventStoreGatewayWorkloadTransportTests
{
    /// <summary>Ordinary client calls opt into scoped workload transport without forwarding human authority.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingClientCallsUseWorkloadRoutesWithExactIndependentGrants(bool command)
    {
        var capture = new CapturingHandler();
        await using ServiceProvider services = Build(capture);
        IEventStoreGatewayClient client = services.GetRequiredService<IEventStoreGatewayClient>();
        if (command)
        {
            _ = await client.SubmitCommandAsync(new SubmitCommandRequest("message", "tenant-a", "work", "item-1", "ResumeWorkItem",
                JsonSerializer.SerializeToElement(new { })), TestContext.Current.CancellationToken);
        }
        else
        {
            _ = await client.ReadStreamAsync(new StreamReadRequest("tenant-a", "work", "item-1"), TestContext.Current.CancellationToken);
        }

        capture.Path.ShouldBe(command ? "/api/v1/commands/workload" : "/api/v1/streams/read/workload");
        capture.Authorization.ShouldBeNull();
        var token = new JsonWebToken(capture.Assertion.ShouldNotBeNull());
        token.Audiences.ShouldBe(["eventstore"]);
        token.GetClaim(EventStoreWorkloadAuthenticationDefaults.CallerClaimType).Value.ShouldBe("works-reader");
        token.GetClaim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType).Value.ShouldBe(command
            ? EventStoreGatewayWorkloadOperations.CommandSubmit : EventStoreGatewayWorkloadOperations.StreamRead);
        token.GetClaim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType).Value.ShouldBe("tenant-a");
        token.GetClaim(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType).Value.ShouldBe("work");
        token.TryGetClaim("global_admin", out _).ShouldBeFalse();
    }

    /// <summary>Unavailable authority never falls back to an unbound or human request.</summary>
    [Fact]
    public async Task MissingIssuerAndUnrelatedOperationsCannotFallBackToHumanTransport()
    {
        var capture = new CapturingHandler();
        await using ServiceProvider services = Build(capture, configured: false);
        IEventStoreGatewayClient client = services.GetRequiredService<IEventStoreGatewayClient>();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => client.ReadStreamAsync(
            new StreamReadRequest("tenant-a", "work", "item-1"), TestContext.Current.CancellationToken));
        capture.Path.ShouldBeNull();
        (await services.GetRequiredService<IEventStoreGatewayWorkloadAssertionSource>().IssueAsync(
            EventStoreWorkloadOperations.TrustedEffect, "tenant-a", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    private static ServiceProvider Build(CapturingHandler capture, bool configured = true)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Issuer"] = "hexalith-dev",
            ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
            ["Authentication:JwtBearer:SigningKey"] = configured ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) : null,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
            ["Authentication:WorkloadIssuer:Workload"] = "works-reader",
        }).Build();
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton(configuration);
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        _ = services.AddSingleton(environment);
        _ = services.AddEventStoreGatewayClient(options => options.BaseAddress = new Uri("http://localhost/"))
            .AddEventStoreGatewayWorkloadTransport("work")
            .ConfigurePrimaryHttpMessageHandler(() => capture);
        return services.BuildServiceProvider();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public string? Assertion { get; private set; }
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath;
            Authorization = request.Headers.Authorization?.ToString();
            Assertion = request.Headers.GetValues(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName).Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Path.Contains("commands", StringComparison.Ordinal)
                    ? JsonContent.Create(new SubmitCommandResponse("correlation", MessageId: "message"))
                    : JsonContent.Create(new StreamReadPage("tenant-a", "work", "item-1", [], new(0, null, null, 0, 0, false, null))),
            });
        }
    }
}
