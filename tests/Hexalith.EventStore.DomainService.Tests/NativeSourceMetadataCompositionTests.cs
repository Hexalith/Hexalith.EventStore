using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Cryptography;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Qualifies independent public registration without changing the ordinary gateway's identity or options.</summary>
public sealed class NativeSourceMetadataCompositionTests
{
    /// <summary>The native-only client signs its own exact source request while the default human client stays separate.</summary>
    [Theory]
    [InlineData("valid")]
    [InlineData("missing-key")]
    [InlineData("bad-key")]
    [InlineData("foreign-domain")]
    public async Task SeparateMetadataClientPreservesDefaultGatewayAndRefusesUnprovableScope(string scenario)
    {
        using RSA key = RSA.Create(2048);
        string sidecarToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        byte[] malformedKeyBytes = RandomNumberGenerator.GetBytes(32);
        malformedKeyBytes[0] = 0; // A PKCS#8 DER sequence cannot start with this byte.
        string? boundReadKey = Convert.ToBase64String(key.ExportPkcs8PrivateKey());
        if (scenario == "missing-key")
        {
            boundReadKey = null;
        }
        else if (scenario == "bad-key")
        {
            boundReadKey = Convert.ToBase64String(malformedKeyBytes);
        }
        var identity = new AggregateIdentity("tenant-a", scenario == "foreign-domain" ? "other-domain" : "folders", "organization-a");
        var proof = NativeSourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500",
            ["DAPR_API_TOKEN"] = sidecarToken,
            ["EventStore:Gateway:AppId"] = "eventstore-isolated",
            ["Authentication:WorkloadIssuer:Workload"] = "folders-isolated",
            ["Authentication:BoundStreamRead:PrivateKey"] = boundReadKey,
        }).Build();
        using var metadataTransport = new MockHttpMessageHandler(request =>
        {
            request.RequestUri!.AbsoluteUri.ShouldBe("http://localhost:3500/api/v1/streams/read/workload");
            request.Headers.GetValues("dapr-app-id").Single().ShouldBe("eventstore-isolated");
            request.Headers.GetValues("dapr-api-token").Single().ShouldBe(sidecarToken);
            request.Headers.Authorization.ShouldBeNull();
            request.Headers.GetValues(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName).Single().ShouldBe("component-workload-assertion");
            var sent = JsonSerializer.Deserialize<StreamReadRequest>(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult(), JsonSerializerOptions.Web)!;
            sent.ShouldBe(new StreamReadRequest(identity.TenantId, "folders", identity.AggregateId,
                FromSequence: 0, ToSequence: 0, PageSize: 1) { RequireNativeProof = true });
            BoundStreamReadProof.Verify(request.Headers.GetValues(BoundStreamReadProof.HeaderName).Single(), sent,
                "folders-isolated", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), DateTimeOffset.UtcNow).ShouldBeTrue();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new StreamReadPage(identity.TenantId, identity.Domain, identity.AggregateId, [],
                    new StreamReadMetadata(0, 0, null, 0, 0, false, null) { NativeProof = proof })),
            };
        });
        using var humanTransport = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("component-workload-assertion"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(issuer);
        services.AddEventStoreGatewayClient().ConfigureHttpClient(client => client.BaseAddress = new Uri("http://human-gateway/"))
            .ConfigurePrimaryHttpMessageHandler(() => humanTransport);
        services.AddEventStoreGatewayNativeSourceMetadataReader("folders")
            .ConfigurePrimaryHttpMessageHandler(() => metadataTransport);
        await using ServiceProvider provider = services.BuildServiceProvider();

        NativeSourceObservation? observation = await provider.GetRequiredService<INativeSourceMetadataReader>()
            .ReadAsync(identity, TestContext.Current.CancellationToken);

        provider.GetRequiredService<IOptions<EventStoreGatewayClientOptions>>().Value.UseWorkloadTransport.ShouldBeFalse();
        provider.GetService<IEventStoreGatewayWorkloadAssertionSource>().ShouldBeNull();
        provider.GetService<IEventStoreGatewayStreamReadProofSource>().ShouldBeNull();
        humanTransport.CallCount.ShouldBe(0);
        if (scenario == "valid")
        {
            observation.ShouldNotBeNull().Proof.ShouldBe(proof);
            metadataTransport.CallCount.ShouldBe(1);
            await issuer.Received(1).IssueAsync(Arg.Is<WorkloadAssertionRequest>(request => request.Audience == "eventstore"
                && request.Operation == EventStoreWorkloadOperations.GatewayStreamRead && request.Bindings != null
                && request.Bindings.Count == 2
                && request.Bindings[EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] == identity.TenantId
                && request.Bindings[EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] == identity.Domain), Arg.Any<CancellationToken>());
        }
        else
        {
            observation.ShouldBeNull();
            metadataTransport.CallCount.ShouldBe(0);
            issuer.ReceivedCalls().ShouldBeEmpty();
        }
        (await provider.GetRequiredService<IEventStoreGatewayClient>().GetCommandStatusAsync("message", TestContext.Current.CancellationToken)).ShouldBeNull();
        humanTransport.CallCount.ShouldBe(1);
    }
}
