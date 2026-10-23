using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed partial class QueryAuthorityCorrectionTests
{
    [Fact]
    public async Task ActualOwnerClientPreservesSdkVerifiedDelegatedOriginThroughTheSignedHttpRequest()
    {
        var origin = new OriginatingQueryContext("actor", "original-client", true, "original-delegation", ["projects.read"], ["public-gateway"]);
        QueryEnvelope query = Query() with
        {
            IsDelegated = true, AuthenticatedWorkloadId = "projects", Audience = ["eventstore"],
            CallerAuthority = new("delegated-owner-query", [], ["read_metadata"]) { OriginatingContext = origin },
        };
        DefaultHttpContext context = Context(query);
        context.Request.Headers[EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName] = "verified-delegated-assertion";
        var accessor = new HttpContextAccessor { HttpContext = context };
        using RSA key = RSA.Create(2048);
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:WorkloadIssuer:Workload"] = "conversations",
            ["Authentication:BoundStreamRead:PrivateKey"] = Convert.ToBase64String(key.ExportPkcs8PrivateKey()),
        }).Build();
        using DaprClient dapr = new DaprClientBuilder().Build();
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>()).Returns("next-hop-workload-assertion");
        using var transport = new OwnerStatusResponseHandler(HttpStatusCode.OK);
        using var http = new HttpClient(transport);
        IHttpClientFactory clients = Substitute.For<IHttpClientFactory>(); clients.CreateClient(Arg.Any<string>()).Returns(http);
        var client = new DelegatedOwnerQueryClient(dapr, clients, issuer, accessor, configuration, TimeProvider.System);
        IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
        handler.Domain.Returns(query.Domain); handler.QueryType.Returns(query.QueryType);
        var ownerQuery = new SubmitQueryRequest("tenant", "tenants", "tenant", "read-authority");
        handler.ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            VerifiedQueryCaller.Get(context).ShouldNotBeNull().OriginatingContext.ShouldBeSameAs(origin);
            return client.InvokeAsync(ownerQuery, TestContext.Current.CancellationToken);
        });
        await using ServiceProvider provider = new ServiceCollection().AddSingleton<IHttpContextAccessor>(accessor).AddSingleton(handler).BuildServiceProvider();
        QueryResult result = await DomainQueryDispatcher.ExecuteAsync(provider, query, TestContext.Current.CancellationToken);
        result.Success.ShouldBeTrue(); result.GetPayload().GetProperty("allowed").GetBoolean().ShouldBeTrue();
        var sent = transport.Request.ShouldNotBeNull();
        sent.Query.ShouldBe(ownerQuery); sent.PrincipalId.ShouldBe("actor");
        JsonSerializer.Serialize(sent.OriginatingQuery).ShouldBe(JsonSerializer.Serialize(query));
        var preserved = sent.OriginatingQuery!.CallerAuthority!.OriginatingContext.ShouldNotBeNull();
        preserved.OriginalActorId.ShouldBe(origin.OriginalActorId); preserved.AuthenticatedWorkloadId.ShouldBe(origin.AuthenticatedWorkloadId);
        preserved.IsDelegated.ShouldBeTrue(); preserved.DelegationId.ShouldBe(origin.DelegationId);
        preserved.Scopes.ShouldBe(origin.Scopes); preserved.Audience.ShouldBe(origin.Audience);
        sent.OriginatingAssertion.ShouldBe("verified-delegated-assertion");
        transport.Assertion.ShouldBe("next-hop-workload-assertion");
        BoundOwnerQueryProof.Verify(sent, transport.Proof, "conversations", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), DateTimeOffset.UtcNow).ShouldBeTrue();
        VerifiedQueryCaller.Get(context).ShouldBeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)] [InlineData(HttpStatusCode.Forbidden)] [InlineData(HttpStatusCode.NotFound)]
    public async Task OwnerTransportAuthenticationFailureIsUnavailableAndOwnerNotFoundRemainsDenied(HttpStatusCode status)
    {
        QueryEnvelope query = Query(); DefaultHttpContext context = Context(query);
        context.Request.Headers[EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName] = "verified-original-assertion";
        var accessor = new HttpContextAccessor { HttpContext = context };
        using RSA key = RSA.Create(2048);
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:WorkloadIssuer:Workload"] = "projects",
            ["Authentication:BoundStreamRead:PrivateKey"] = Convert.ToBase64String(key.ExportPkcs8PrivateKey()),
        }).Build();
        using DaprClient dapr = new DaprClientBuilder().Build();
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>()).Returns("workload-assertion");
        using var http = new HttpClient(new OwnerStatusResponseHandler(status));
        IHttpClientFactory clients = Substitute.For<IHttpClientFactory>(); clients.CreateClient(Arg.Any<string>()).Returns(http);
        var client = new DelegatedOwnerQueryClient(dapr, clients, issuer, accessor, configuration, TimeProvider.System);
        IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
        handler.Domain.Returns(query.Domain); handler.QueryType.Returns(query.QueryType);
        handler.ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(_ => client.InvokeAsync(new("tenant", "folders", "folder", "read")));
        await using ServiceProvider provider = new ServiceCollection().AddSingleton<IHttpContextAccessor>(accessor).AddSingleton(handler).BuildServiceProvider();
        QueryResult result = await DomainQueryDispatcher.ExecuteAsync(provider, query);
        result.ErrorMessage.ShouldBe(status == HttpStatusCode.NotFound ? "safe-denial" : QueryAdapterFailureReason.QueryExecutionFailure);
        result.PayloadBytes.ShouldBeNull();
    }
}
