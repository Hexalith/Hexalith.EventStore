using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.QueryRouting.Tests;

public sealed class DaprDomainQueryInvokerTests {
    /// <summary>
    /// FR28: the caller's human bearer never reaches a domain service. The platform outbound handler supplies
    /// EventStore's own workload assertion instead.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_DoesNotForwardInboundBearerCredential() {
        using DaprClient daprClient = new DaprClientBuilder().Build();
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        _ = resolver
            .ResolveAsync("tenant-a", "projects", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration("projects-app", "process", "tenant-a", "projects", null));
        var capture = new QueryRequestCaptureHandler();
        using var httpClient = new HttpClient(capture);
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);
        var invoker = new DaprDomainQueryInvoker(
            daprClient,
            httpClientFactory,
            resolver,
            NullLogger<DaprDomainQueryInvoker>.Instance);

        _ = await invoker.InvokeAsync(Query(), TestContext.Current.CancellationToken);

        capture.Authorization.ShouldBeNull();
        capture.RequestUri!.AbsolutePath.ShouldEndWith("/invoke/projects-app/method/query");
    }

    private static QueryEnvelope Query()
        => new(
            "tenant-a",
            "projects",
            "project-1",
            "projects.context.get.v1",
            JsonSerializer.SerializeToUtf8Bytes(new { projectId = "project-1" }),
            "correlation-1",
            "actor-1");
}
