using System.Net;
using System.Security.Cryptography;
using Dapr.Client;
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
        result.ErrorMessage.ShouldBe(status == HttpStatusCode.NotFound ? "safe-denial" : QueryAdapterFailureReason.ActorException);
        result.PayloadBytes.ShouldBeNull();
    }
}
