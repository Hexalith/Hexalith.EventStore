using System.Security.Claims;
using System.Security.Cryptography;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Fakes;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.EventStore.Testing.Http;
using Hexalith.EventStore.Testing.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>Native Source Scanner Authorization Tests.</summary>
public sealed class NativeSourceScannerAuthorizationTests
{
    /// <summary>Exact Scan Requires Issuer Bindings Separate Enrollment And Page Proof Before Source.</summary>
    [Fact]
    public async Task ExactScanRequiresIssuerBindingsSeparateEnrollmentAndPageProofBeforeSource()
    {
        using RSA key = RSA.Create(2048);
        const string caller = "folders";
        const string tenant = "tenant-a";
        const string domain = "folders";
        const string channel = "scan-channel";
        var identity = new AggregateIdentity(tenant, domain, "folder-a");
        var proof = SourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var inspection = new SourceProofInspection(SourceProofInspectionStatus.Available, true, 0, false, proof);
        var actor = Substitute.For<IAggregateActor>(); actor.InspectSourceProofAsync().Returns(inspection);
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor", Arg.Any<ActorProxyOptions?>()).Returns(actor);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [DaprAppChannelToken.ConfigurationKey] = channel,
            ["Authentication:JwtBearer:Issuer"] = WorkloadAssertionTestTokens.Issuer,
            ["Authentication:JwtBearer:Audience"] = DaprInternalAuthenticationOptions.DefaultAudience,
            ["Authentication:JwtBearer:SigningKey"] = AuthenticationTestEnvironment.SigningKey,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
            ["Authentication:NativeSourceScan:PublicKeys:folders"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            ["Authentication:NativeSourceScan:AllowedDomains:folders"] = domain,
            ["Authentication:BoundStreamRead:MetadataOnlyCallers:folders"] = "true",
            ["Authentication:BoundStreamRead:PublicKeys:folders"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            ["Authentication:BoundStreamRead:AllowedDomains:folders"] = domain,
        });
        builder.Services.AddAuthentication(DaprInternalAuthenticationOptions.SchemeName)
            .AddEventStoreWorkloadScheme(DaprInternalAuthenticationOptions.SchemeName, "Authentication:DaprInternal", (options, _) =>
            { options.Audience = DaprInternalAuthenticationOptions.DefaultAudience; options.AllowedCallers = [caller]; });
        builder.Services.AddEventStoreWorkloadPolicies(DaprInternalAuthenticationOptions.SchemeName,
            [EventStoreWorkloadOperations.GatewayNativeSourceScan, EventStoreWorkloadOperations.GatewayStreamRead]);
        builder.Services.AddControllers().AddApplicationPart(typeof(StreamsController).Assembly);
        builder.Services.AddSingleton<IActorProxyFactory>(proxies);
        builder.Services.AddSingleton<Hexalith.EventStore.Authorization.ITenantValidator>(new FakeTenantValidator());
        builder.Services.AddSingleton<IRbacValidator>(new FakeRbacValidator());
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var http = app.GetTestClient(); http.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, channel);
        var request = new StreamReadRequest(tenant, domain, identity.AggregateId, 0, 0, PageSize: 1) { RequireNativeProof = true };
        foreach (string scenario in new[] { "valid", "operation", "caller", "tenant", "domain", "missing-binding", "missing-proof", "ordinary-proof", "page", "target", "human" })
        {
            var sent = scenario switch { "page" => request with { ToSequence = 1 }, "target" => request with { AggregateId = "folder-b" }, _ => request };
            var bindings = scenario == "missing-binding" ? null : new Dictionary<string, string>
            {
                [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = scenario == "tenant" ? "tenant-b" : tenant,
                [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = scenario == "domain" ? "other-domain" : domain,
            };
            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/streams/scan/workload") { Content = JsonContent.Create(sent) };
            message.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName,
                WorkloadAssertionTestTokens.Create(DaprInternalAuthenticationOptions.DefaultAudience, scenario == "caller" ? "unenrolled" : caller,
                    [scenario == "operation" ? EventStoreWorkloadOperations.GatewayStreamRead : EventStoreWorkloadOperations.GatewayNativeSourceScan], bindings: bindings));
            if (scenario != "missing-proof") message.Headers.Add(BoundNativeSourceScanProof.HeaderName,
                scenario == "ordinary-proof" ? BoundStreamReadProof.Issue(request, caller, Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow)
                : BoundNativeSourceScanProof.Issue(request, caller, Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow));
            if (scenario == "human") message.Headers.Authorization = new("Bearer", "human");
            int before = proxies.ReceivedCalls().Count();
            using var response = await http.SendAsync(message, TestContext.Current.CancellationToken);
            if (scenario == "valid") response.StatusCode.ShouldBe(HttpStatusCode.OK);
            else
            {
                response.StatusCode.ShouldBe(scenario is "caller" or "human" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
                proxies.ReceivedCalls().Count().ShouldBe(before);
            }
        }
        // The same enrolled caller remains metadata-only on the ordinary workload operation.
        var range = request with { ToSequence = 1 };
        using var old = new HttpRequestMessage(HttpMethod.Post, "/api/v1/streams/read/workload") { Content = JsonContent.Create(range) };
        old.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName,
            WorkloadAssertionTestTokens.Create(DaprInternalAuthenticationOptions.DefaultAudience, caller, [EventStoreWorkloadOperations.GatewayStreamRead],
                bindings: new Dictionary<string, string> { [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = tenant,
                    [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = domain }));
        old.Headers.Add(BoundStreamReadProof.HeaderName, BoundStreamReadProof.Issue(range, caller, Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow));
        int sourceCalls = proxies.ReceivedCalls().Count();
        using var refused = await http.SendAsync(old, TestContext.Current.CancellationToken);
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden); proxies.ReceivedCalls().Count().ShouldBe(sourceCalls);
    }
}
