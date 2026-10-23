using System.Net;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.DomainServices;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;

public sealed class DaprDomainQueryInvokerFailureTests
{
    [Theory]
    [InlineData(400, "{\"success\":false,\"errorMessage\":\"private remote detail\"}")]
    [InlineData(400, "not-json")]
    [InlineData(200, "not-json")]
    [InlineData(503, "private remote detail")]
    [InlineData(0, "private transport exception")]
    public async Task UnexpectedRemoteFailuresUseOnlyBoundedSentinel(int status, string body)
    {
        using DaprClient dapr = new DaprClientBuilder().Build();
        using var client = new HttpClient(new QueryInvokerResponseHandler(() => status == 0
            ? throw new HttpRequestException(body) : new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") }));
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>(); factory.CreateClient(Arg.Any<string>()).Returns(client);
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration("projects", "process", "tenant", "projects", null));
        var invoker = new DaprDomainQueryInvoker(dapr, factory, resolver, NullLogger<DaprDomainQueryInvoker>.Instance);
        QueryResult result = await invoker.InvokeAsync(new("tenant", "projects", "project", "read", [], "corr", "actor"));
        result.Success.ShouldBeFalse(); result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.QueryExecutionFailure);
    }
}
