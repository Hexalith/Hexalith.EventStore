using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed partial class DomainServiceTrustBoundaryTests
{
    /// <summary>Checks the SDK default and that an opted-in policy runs after authorization, verification and dispatch.</summary>
    [Theory]
    [InlineData("default", HttpStatusCode.OK)]
    [InlineData("mapped", HttpStatusCode.BadRequest)]
    [InlineData("operation", HttpStatusCode.Forbidden)]
    [InlineData("catalog", HttpStatusCode.ServiceUnavailable)]
    [InlineData("verifier", HttpStatusCode.ServiceUnavailable)]
    public async Task QueryResponsePolicyRunsAfterAdmittedDispatch(string scenario, HttpStatusCode status)
    {
        QueryResult failure = QueryResult.Failure("fixture-invalid");
        List<string> calls = [];
        IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
        handler.Domain.Returns("widget");
        handler.QueryType.Returns("response-policy");
        handler.ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(arguments =>
        {
            arguments.Arg<QueryEnvelope>().IsGlobalAdmin.ShouldBeFalse();
            calls.Add("handler");
            return failure;
        });
        IDomainQueryResponsePolicy policy = Substitute.For<IDomainQueryResponsePolicy>();
        policy.CreateResponse(Arg.Any<QueryEnvelope>(), Arg.Any<QueryResult>()).Returns(arguments =>
        {
            arguments.Arg<QueryEnvelope>().IsGlobalAdmin.ShouldBeTrue();
            arguments.Arg<QueryResult>().ShouldBeSameAs(failure);
            calls.Add("policy");
            return Results.BadRequest(failure);
        });
        await using var app = await StartAsync(configureServices: services =>
        {
            services.AddScoped(_ => handler);
            if (scenario != "default")
                services.AddSingleton(policy);
            if (scenario == "catalog")
                services.AddScoped<IDomainRouteCatalogGuard>(_ => new DomainRouteCatalogIsolatedGuard(deny: true));
            services.AddSingleton<IDomainServiceAdministratorVerifier>(new DelegateVerifier(_ =>
            {
                calls.Add("verifier");
                return scenario == "verifier" ? throw new InvalidOperationException("Fixture verifier unavailable.") : false;
            }));
        });
        var query = new QueryEnvelope("tenant-a", "widget", "widget-1", "response-policy", [], "corr-q", "acting-user", isGlobalAdmin: true);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/query")
        {
            Content = JsonContent.Create(query, options: JsonSerializerOptions.Web),
        };
        AddInternalHeaders(request, Assertion([scenario == "operation"
            ? EventStoreWorkloadOperations.DomainServiceProcess : EventStoreWorkloadOperations.DomainServiceQuery]), [ChannelToken]);
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(status);
        switch (scenario)
        {
            case "default": calls.ShouldBe(["verifier", "handler"]); break;
            case "mapped": calls.ShouldBe(["verifier", "handler", "policy"]); break;
            case "verifier": calls.ShouldBe(["verifier"]); break;
            default: calls.ShouldBeEmpty(); break;
        }
        if (scenario is "default" or "mapped")
        {
            (await response.Content.ReadFromJsonAsync<QueryResult>(TestContext.Current.CancellationToken)).ShouldBe(failure);
        }
    }
}
