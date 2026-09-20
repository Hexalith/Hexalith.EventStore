using System.Net.Http.Headers;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.QueryRouting.Tests;

public sealed class DaprDomainQueryInvokerTests {
    private const string BearerCredential = "Bearer exact-token-value";

    [Fact]
    public async Task InvokeAsync_ForwardsExactInboundBearerCredential() {
        using DaprClient daprClient = new DaprClientBuilder().Build();
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        _ = resolver
            .ResolveAsync("tenant-a", "projects", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration("projects-app", "process", "tenant-a", "projects", null));
        var capture = new QueryRequestCaptureHandler();
        using var httpClient = new HttpClient(capture);
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = BearerCredential;
        var invoker = new DaprDomainQueryInvoker(
            daprClient,
            httpClientFactory,
            resolver,
            new HttpContextAccessor { HttpContext = context },
            NullLogger<DaprDomainQueryInvoker>.Instance);

        _ = await invoker.InvokeAsync(Query(), TestContext.Current.CancellationToken);

        capture.Authorization.ShouldBe(BearerCredential);
    }

    [Fact]
    public void ForwardBearerCredential_MissingContextOrHeader_DoesNotAddAuthorization() {
        using var noContextRequest = new HttpRequestMessage(HttpMethod.Post, "http://localhost/query");
        DaprDomainQueryInvoker.ForwardBearerCredential(null, noContextRequest);
        noContextRequest.Headers.Authorization.ShouldBeNull();

        using var noHeaderRequest = new HttpRequestMessage(HttpMethod.Post, "http://localhost/query");
        DaprDomainQueryInvoker.ForwardBearerCredential(new DefaultHttpContext(), noHeaderRequest);
        noHeaderRequest.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public void ForwardBearerCredential_ExistingOutboundAuthorization_IsPreserved() {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = BearerCredential;
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/query");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "existing-token");

        DaprDomainQueryInvoker.ForwardBearerCredential(context, request);

        request.Headers.Authorization!.ToString().ShouldBe("Bearer existing-token");
    }

    [Fact]
    public void ForwardBearerCredential_ExistingUnparsedOutboundAuthorization_IsPreserved() {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = BearerCredential;
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/query");
        _ = request.Headers.TryAddWithoutValidation("Authorization", "custom-value");

        DaprDomainQueryInvoker.ForwardBearerCredential(context, request);

        request.Headers.GetValues("Authorization").ShouldBe(["custom-value"]);
    }

    [Fact]
    public void ForwardBearerCredential_NonBearerCredential_IsIgnored() {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Basic dXNlcjpwYXNz";
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/query");

        DaprDomainQueryInvoker.ForwardBearerCredential(context, request);

        request.Headers.Authorization.ShouldBeNull();
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
