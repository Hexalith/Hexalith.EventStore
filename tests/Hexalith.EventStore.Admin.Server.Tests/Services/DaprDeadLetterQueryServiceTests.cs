using System.Net;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

/// <summary>
/// Verifies dead-letter queries route to the caller-scoped operations workload.
/// </summary>
public sealed class DaprDeadLetterQueryServiceTests {
    /// <summary>Verifies count uses operations service invocation and forwards the JWT.</summary>
    [Fact]
    public async Task GetDeadLetterCountAsyncRoutesToOperationsAndForwardsJwt() {
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        _ = authContext.GetToken().Returns("operator-token");
        (DaprDeadLetterQueryService service, TestHttpMessageHandler handler) = CreateService(authContext);
        handler.SetupJsonResponse(3);

        int count = await service.GetDeadLetterCountAsync();

        count.ShouldBe(3);
        HttpRequestMessage request = handler.LastRequest.ShouldNotBeNull();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri.ShouldNotBeNull().AbsolutePath.ShouldBe("/internal/dead-letters/count");
        request.Headers.Authorization.ShouldNotBeNull().Parameter.ShouldBe("operator-token");
    }

    /// <summary>Verifies tenant and paging values are forwarded without reading state directly.</summary>
    [Fact]
    public async Task ListDeadLettersAsyncForwardsTenantAndPagingToOperations() {
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        _ = authContext.GetToken().Returns("operator-token");
        (DaprDeadLetterQueryService service, TestHttpMessageHandler handler) = CreateService(authContext, out DaprClient daprClient);
        var expected = new PagedResult<DeadLetterEntry>(
            [new DeadLetterEntry("message-a", "tenant-a", "work", "work-a", "correlation-a", "retained", DateTimeOffset.UtcNow, 0, "WorkItemCreated")],
            2,
            "1");
        handler.SetupJsonResponse(expected);

        PagedResult<DeadLetterEntry> result = await service.ListDeadLettersAsync("tenant-a", 1, "0");

        result.TotalCount.ShouldBe(2);
        Uri uri = handler.LastRequest.ShouldNotBeNull().RequestUri.ShouldNotBeNull();
        uri.AbsolutePath.ShouldBe("/internal/dead-letters");
        uri.Query.ShouldContain("tenantId=tenant-a");
        uri.Query.ShouldContain("count=1");
        uri.Query.ShouldContain("continuationToken=0");
        handler.LastRequest.Headers.Authorization.ShouldNotBeNull().Parameter.ShouldBe("operator-token");
        _ = daprClient.Received(1).CreateInvokeMethodRequest(
            HttpMethod.Get,
            "eventstore-operations",
            "internal/dead-letters",
            Arg.Any<IReadOnlyCollection<KeyValuePair<string, string>>>());
    }

    /// <summary>Verifies a non-null whitespace tenant cannot silently become an admin-wide query.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ListDeadLettersAsyncRejectsWhitespaceTenant(string tenantId) {
        (DaprDeadLetterQueryService service, _) = CreateService(new NullAdminAuthContext());

        _ = await Should.ThrowAsync<ArgumentException>(
            () => service.ListDeadLettersAsync(tenantId, 10, null));
    }

    /// <summary>Verifies service unavailability is propagated to the existing controller mapping.</summary>
    [Fact]
    public async Task ListDeadLettersAsyncPropagatesServiceUnavailable() {
        (DaprDeadLetterQueryService service, TestHttpMessageHandler handler) = CreateService(new NullAdminAuthContext());
        handler.SetupErrorResponse(HttpStatusCode.ServiceUnavailable);

        _ = await Should.ThrowAsync<HttpRequestException>(
            () => service.ListDeadLettersAsync("tenant-a", 10, null));
    }

    /// <summary>Verifies caller cancellation is not rewritten as an operations timeout.</summary>
    [Fact]
    public async Task ListDeadLettersAsyncPropagatesCallerCancellation() {
        using CancellationTokenSource source = new();
        await source.CancelAsync();
        (DaprDeadLetterQueryService service, TestHttpMessageHandler handler) = CreateService(new NullAdminAuthContext());
        handler.SetupException(new OperationCanceledException(source.Token));

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.ListDeadLettersAsync("tenant-a", 10, null, source.Token));
    }

    /// <summary>
    /// Verifies an authorization refusal from the operations workload is canonicalized on the read surfaces.
    /// </summary>
    /// <remarks>
    /// The command facade already maps 401/403 to <c>Unauthorized</c> (DW11 AC4). Without the same mapping here,
    /// <c>EnsureSuccessStatusCode</c> raises <see cref="HttpRequestException"/>, which
    /// <c>AdminDeadLettersController</c> classifies as a backend outage: the operator is told the service is
    /// temporarily unavailable and to retry, for a refusal no retry can clear.
    /// </remarks>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ReadSurfacesCanonicalizeAuthorizationRefusals(HttpStatusCode status) {
        (DaprDeadLetterQueryService service, TestHttpMessageHandler handler) = CreateService(new NullAdminAuthContext());
        handler.SetupErrorResponse(status);

        _ = await Should.ThrowAsync<UnauthorizedAccessException>(
            () => service.ListDeadLettersAsync("tenant-a", 10, null));

        handler.SetupErrorResponse(status);

        _ = await Should.ThrowAsync<UnauthorizedAccessException>(
            () => service.GetDeadLetterCountAsync());
    }

    /// <summary>Verifies an operations workload that never answers becomes the documented timeout.</summary>
    /// <remarks>
    /// Both controller catch filters exclude <see cref="OperationCanceledException"/>, so only the rewrite into
    /// <see cref="TimeoutException"/> keeps a slow backend on the advertised 503 path instead of surfacing as an
    /// unhandled error.
    /// </remarks>
    [Fact]
    public async Task ListDeadLettersAsyncRewritesInternalTimeout() {
        (DaprDeadLetterQueryService service, TestHttpMessageHandler handler) = CreateService(
            new NullAdminAuthContext(),
            out _,
            serviceInvocationTimeoutSeconds: 0);
        handler.SetupException(new OperationCanceledException());

        _ = await Should.ThrowAsync<TimeoutException>(
            () => service.ListDeadLettersAsync("tenant-a", 10, null));
    }

    private static (DaprDeadLetterQueryService Service, TestHttpMessageHandler Handler) CreateService(
        IAdminAuthContext authContext)
        => CreateService(authContext, out _);

    private static (DaprDeadLetterQueryService Service, TestHttpMessageHandler Handler) CreateService(
        IAdminAuthContext authContext,
        out DaprClient daprClient,
        int serviceInvocationTimeoutSeconds = 30) {
        daprClient = Substitute.For<DaprClient>();
        _ = daprClient.CreateInvokeMethodRequest(Arg.Any<HttpMethod>(), "eventstore-operations", Arg.Any<string>())
            .Returns(call => new HttpRequestMessage(
                call.ArgAt<HttpMethod>(0),
                "http://localhost/" + call.ArgAt<string>(2)));
        _ = daprClient.CreateInvokeMethodRequest(
                Arg.Any<HttpMethod>(),
                "eventstore-operations",
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<KeyValuePair<string, string>>>() )
            .Returns(call => {
                string endpoint = call.ArgAt<string>(2);
                IReadOnlyCollection<KeyValuePair<string, string>> query = call.ArgAt<IReadOnlyCollection<KeyValuePair<string, string>>>(3);
                string queryString = string.Join("&", query.Select(static item =>
                    Uri.EscapeDataString(item.Key) + "=" + Uri.EscapeDataString(item.Value)));
                return new HttpRequestMessage(call.ArgAt<HttpMethod>(0), "http://localhost/" + endpoint + "?" + queryString);
            });
        var handler = new TestHttpMessageHandler();
        HttpClient client = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient(Arg.Any<string>()).Returns(client);
        var service = new DaprDeadLetterQueryService(
            daprClient,
            factory,
            Options.Create(new AdminServerOptions {
                OperationsAppId = "eventstore-operations",
                ServiceInvocationTimeoutSeconds = serviceInvocationTimeoutSeconds,
            }),
            authContext,
            NullLogger<DaprDeadLetterQueryService>.Instance);
        return (service, handler);
    }
}
