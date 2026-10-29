using System.Net;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogQueryResponseBoundaryTests
{
    [Theory]
    [InlineData("matching", false)]
    [InlineData("matching", true)]
    [InlineData("projection-mismatch", false)]
    [InlineData("projection-mismatch", true)]
    [InlineData("receiver-503", false)]
    [InlineData("receiver-503", true)]
    [InlineData("transport-catalog", false)]
    [InlineData("transport-catalog", true)]
    public async Task ConcreteQueryDecodePreservesCommittedProjectionAndCatalogRefusalThroughGateway(string response, bool nested)
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        var declaration = fixture.Envelope.Declarations.First(d => d.Kind == "query" && d.Execution!.TargetKind == "domain-service" && !d.Execution.RequiresDelegatedContext);
        var target = fixture.Envelope.Routes.Single(r => r.RouteId == declaration.RouteId);
        using var dapr = new DaprClientBuilder().UseHttpEndpoint("http://localhost:3500").Build();
        int calls = 0;
        using var transport = new MockHttpMessageHandler((request, _) =>
        {
            calls++; request.RequestUri!.AbsolutePath.ShouldBe($"/v1.0/invoke/{target.AppId}/method/{target.Method}");
            if (response == "transport-catalog") throw new RouteCatalogUnavailableException("nested authority refusal");
            return Task.FromResult(new HttpResponseMessage(response == "receiver-503" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(new QueryResult(true, "{\"value\":7}"u8.ToArray(), null,
                response == "projection-mismatch" ? "foreign-projection" : declaration.ProjectionType), JsonSerializerOptions.Web)) });
        });
        using var client = new HttpClient(transport); var clients = Substitute.For<IHttpClientFactory>(); clients.CreateClient(Arg.Any<string>()).Returns(client);
        var resolver = Substitute.For<IDomainServiceResolver>();
        var invoker = new DaprDomainQueryInvoker(fixture.Authorities[0], dapr, clients, resolver, NullLogger<DaprDomainQueryInvoker>.Instance);
        var registry = Substitute.For<IDomainQueryHandlerRegistry>(); var inner = Substitute.For<IQueryRouter>();
        var gateway = new HandlerAwareQueryRouter(fixture.Authorities[0], inner, registry, invoker, NullLogger<HandlerAwareQueryRouter>.Instance);
        string tenant = target.ResolverTenantPattern is null or "*" ? "tenant-a" : target.ResolverTenantPattern;
        async Task<bool> Invoke() => nested
            ? (await gateway.RouteQueryAsync(new SubmitQuery(tenant, declaration.Domain, "aggregate", declaration.MessageType!, [], "correlation", "user"))).Success
            : (await invoker.InvokeAsync(new QueryEnvelope(tenant, declaration.Domain, "aggregate", declaration.MessageType!, [], "correlation", "user"))).Success;
        if (response == "matching") (await Invoke()).ShouldBeTrue();
        else await Should.ThrowAsync<RouteCatalogUnavailableException>(Invoke);
        calls.ShouldBe(1); resolver.ReceivedCalls().ShouldBeEmpty(); registry.ReceivedCalls().ShouldBeEmpty(); inner.ReceivedCalls().ShouldBeEmpty();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }
}
