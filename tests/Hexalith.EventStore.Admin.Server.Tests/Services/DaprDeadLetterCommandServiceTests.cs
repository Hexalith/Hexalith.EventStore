using System.Net;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprDeadLetterCommandServiceTests {
    private const string OperationsAppId = "eventstore-operations";

    private static (DaprDeadLetterCommandService Service, TestHttpMessageHandler Handler) CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null) {
        daprClient ??= Substitute.For<DaprClient>();
        _ = daprClient.CreateInvokeMethodRequest(
                HttpMethod.Post,
                OperationsAppId,
                Arg.Any<string>())
            .Returns(call => new HttpRequestMessage(
                HttpMethod.Post,
                "http://localhost/" + call.ArgAt<string>(2)));
        authContext ??= new NullAdminAuthContext();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            OperationsAppId = OperationsAppId,
            ServiceInvocationTimeoutSeconds = 30,
        });

        var handler = new TestHttpMessageHandler();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new DaprDeadLetterCommandService(
            daprClient,
            httpClientFactory,
            options,
            authContext,
            NullLogger<DaprDeadLetterCommandService>.Instance);

        return (service, handler);
    }

    // === RetryDeadLettersAsync ===

    [Fact]
    public async Task RetryDeadLettersAsync_ReturnsSuccess_WhenEventStoreResponds() {
        var expected = new AdminOperationResult(true, "op-1", "Retry started", null);
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1", "msg-2"]);

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldBe("op-1");
    }

    [Fact]
    public async Task RetryDeadLettersAsync_ForwardsJwtToken() {
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        _ = authContext.GetToken().Returns("dl-token");

        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService(authContext: authContext);
        handler.SetupJsonResponse(new AdminOperationResult(true, "op-1", null, null));

        _ = await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        _ = handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Headers.Authorization!.Parameter.ShouldBe("dl-token");
    }

    [Fact]
    public async Task RetryDeadLettersAsync_ReturnsError_WhenServiceUnavailable() {
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("EventStore down"));

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
        _ = result.ErrorCode.ShouldNotBeNull();
    }

    [Fact]
    public async Task RetryDeadLettersAsync_ReturnsNullResponseError() {
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupNullJsonResponse();

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NULL_RESPONSE");
    }

    [Fact]
    public async Task RetryDeadLettersAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException());

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.RetryDeadLettersAsync("tenant-a", ["msg-1"], cts.Token));
    }

    [Fact]
    public async Task RetryDeadLettersAsync_ReturnsTimeoutError_WhenServiceTimesOut() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.CreateInvokeMethodRequest(HttpMethod.Post, OperationsAppId, Arg.Any<string>())
            .Returns(new HttpRequestMessage(HttpMethod.Post, "http://localhost/internal/dead-letters/retry"));
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            OperationsAppId = OperationsAppId,
            ServiceInvocationTimeoutSeconds = 0,
        });

        var handler = new TestHttpMessageHandler();
        // The handler respects the cancellation token — when timeout is 0s the linked CTS fires immediately
        handler.SetupException(new OperationCanceledException());

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        DaprDeadLetterCommandService service = new(
            daprClient,
            httpClientFactory,
            options,
            new NullAdminAuthContext(),
            NullLogger<DaprDeadLetterCommandService>.Instance);

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("TIMEOUT");
    }

    // === SkipDeadLettersAsync ===

    [Fact]
    public async Task SkipDeadLettersAsync_ReturnsSuccess_WhenEventStoreResponds() {
        var expected = new AdminOperationResult(true, "op-2", "Skip complete", null);
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.SkipDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task SkipDeadLettersAsync_ReturnsError_WhenServiceUnavailable() {
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new HttpRequestException("Connection refused"));

        AdminOperationResult result = await service.SkipDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
    }

    // === ArchiveDeadLettersAsync ===

    [Fact]
    public async Task ArchiveDeadLettersAsync_ReturnsSuccess_WhenEventStoreResponds() {
        var expected = new AdminOperationResult(true, "op-3", "Archive complete", null);
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.ArchiveDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ArchiveDeadLettersAsync_ReturnsError_WhenServiceUnavailable() {
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("EventStore down"));

        AdminOperationResult result = await service.ArchiveDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
    }

    [Theory]
    [InlineData("retry")]
    [InlineData("skip")]
    [InlineData("archive")]
    public async Task ActionsRouteExactlyToOperationsWithTenantIdsAndBearer(string action) {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        _ = authContext.GetToken().Returns("operator-token");
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService(daprClient, authContext);
        handler.SetupJsonResponse(new AdminOperationResult(true, "operation", null, null));

        _ = action switch {
            "retry" => await service.RetryDeadLettersAsync("tenant-a", ["message-a", "message-b"]),
            "skip" => await service.SkipDeadLettersAsync("tenant-a", ["message-a", "message-b"]),
            _ => await service.ArchiveDeadLettersAsync("tenant-a", ["message-a", "message-b"]),
        };

        _ = daprClient.Received(1).CreateInvokeMethodRequest(
            HttpMethod.Post,
            OperationsAppId,
            $"internal/dead-letters/{action}");
        HttpRequestMessage request = handler.LastRequest.ShouldNotBeNull();
        request.RequestUri.ShouldNotBeNull().AbsolutePath.ShouldBe($"/internal/dead-letters/{action}");
        request.Headers.Authorization.ShouldNotBeNull().Parameter.ShouldBe("operator-token");
        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody.ShouldNotBeNull());
        body.RootElement.GetProperty("tenantId").GetString().ShouldBe("tenant-a");
        body.RootElement.GetProperty("messageIds").EnumerateArray()
            .Select(static item => item.GetString())
            .ShouldBe(["message-a", "message-b"]);
    }

    [Theory]
    [InlineData("retry")]
    [InlineData("skip")]
    [InlineData("archive")]
    public async Task ActionsForwardStableActionIdAndPreservePersistedState(string action) {
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(
            new AdminOperationResult(false, "operation-9", "The recovery action state was persisted.", null) { State = "pending" },
            HttpStatusCode.Accepted);

        AdminOperationResult result = action switch {
            "retry" => await service.RetryDeadLettersAsync("tenant-a", ["message-a"], "action-9"),
            "skip" => await service.SkipDeadLettersAsync("tenant-a", ["message-a"], "action-9"),
            _ => await service.ArchiveDeadLettersAsync("tenant-a", ["message-a"], "action-9"),
        };

        result.OperationId.ShouldBe("operation-9");
        result.State.ShouldBe("pending");
        result.Success.ShouldBeFalse();
        handler.LastRequest.ShouldNotBeNull().RequestUri.ShouldNotBeNull().AbsolutePath.ShouldBe($"/internal/dead-letters/{action}");
        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody.ShouldNotBeNull());
        body.RootElement.GetProperty("tenantId").GetString().ShouldBe("tenant-a");
        body.RootElement.GetProperty("actionId").GetString().ShouldBe("action-9");
        body.RootElement.GetProperty("messageIds").EnumerateArray().Select(static item => item.GetString()).ShouldBe(["message-a"]);
    }

    [Theory]
    [InlineData("retry")]
    [InlineData("skip")]
    [InlineData("archive")]
    public async Task ActionsWithoutActionIdSendOneFreshValidIdentityPerCall(string action) {
        // Without an action ID Operations derives a fixed identity, so a retry after a failed campaign would only read
        // back the stored failure. Each legacy call is a new decision and must carry its own identity.
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(new AdminOperationResult(true, "operation", null, null));
        var sent = new List<string>();

        for (int call = 0; call < 2; call++) {
            _ = action switch {
                "retry" => await service.RetryDeadLettersAsync("tenant-a", ["message-a"]),
                "skip" => await service.SkipDeadLettersAsync("tenant-a", ["message-a"]),
                _ => await service.ArchiveDeadLettersAsync("tenant-a", ["message-a"]),
            };
            using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody.ShouldNotBeNull());
            sent.Add(body.RootElement.GetProperty("actionId").GetString().ShouldNotBeNull());
        }

        handler.RequestCount.ShouldBe(2);
        sent.Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
        sent.ShouldAllBe(id => id.Length > 0 && id.Length <= 128 && id == id.Trim() && !id.Any(char.IsControl));
    }

    [Fact]
    public async Task ActionsReportUnavailableRecoveryProfileWithoutRawDiagnostics() {
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new HttpRequestException("private sidecar diagnostic", null, HttpStatusCode.ServiceUnavailable));

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["message-a"], "action-10");

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("Unavailable");
        result.Message.ShouldNotBeNull().ShouldNotContain("private sidecar diagnostic");
    }

    // === Error code extraction ===

    [Fact]
    public async Task InvokePost_MapsHttpStatusCode_FromHttpRequestException() {
        // 404 must canonicalize to "NotFound" so AdminDeadLettersController surfaces a recoverable
        // 404 ProblemDetails for visual-fixture DLQ misses (DW11 AC4) instead of falling through
        // to 500.
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupErrorResponse(HttpStatusCode.NotFound);

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NotFound");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, "Unauthorized")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "InvalidOperation")]
    public async Task InvokePost_CanonicalizesAuthorizationAndValidationStatuses(
        HttpStatusCode status,
        string expectedErrorCode) {
        // The operations workload answers an unauthorized caller with 403 and a rejected action with 422.
        // Both must canonicalize (DW11 AC4) so AdminDeadLettersController surfaces the matching ProblemDetails
        // instead of a blanket 500 that hides an authorization failure as a server fault.
        (DaprDeadLetterCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupErrorResponse(status);

        AdminOperationResult result = await service.RetryDeadLettersAsync("tenant-a", ["msg-1"]);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe(expectedErrorCode);
    }
}
