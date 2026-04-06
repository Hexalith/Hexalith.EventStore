using System.Net;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprBackupCommandServiceTests
{
    private const string EventStoreAppId = "eventstore";

    private static (DaprBackupCommandService Service, TestHttpMessageHandler Handler) CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null)
    {
        daprClient ??= Substitute.For<DaprClient>();
        authContext ??= new NullAdminAuthContext();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions
        {
            EventStoreAppId = EventStoreAppId,
            ServiceInvocationTimeoutSeconds = 30,
        });

        var handler = new TestHttpMessageHandler();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new DaprBackupCommandService(
            daprClient,
            httpClientFactory,
            options,
            authContext,
            NullLogger<DaprBackupCommandService>.Instance);

        return (service, handler);
    }

    // === TriggerBackupAsync ===

    [Fact]
    public async Task TriggerBackupAsync_ReturnsSuccess_WhenEventStoreResponds()
    {
        var expected = new AdminOperationResult(true, "op-1", "Backup started", null);
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.TriggerBackupAsync("tenant-a", "nightly", true);

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldBe("op-1");
    }

    [Fact]
    public async Task TriggerBackupAsync_ForwardsJwtToken()
    {
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        authContext.GetToken().Returns("backup-token");

        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService(authContext: authContext);
        handler.SetupJsonResponse(new AdminOperationResult(true, "op-1", null, null));

        await service.TriggerBackupAsync("tenant-a", null, false);

        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Headers.Authorization!.Parameter.ShouldBe("backup-token");
    }

    [Fact]
    public async Task TriggerBackupAsync_ReturnsError_WhenServiceUnavailable()
    {
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("EventStore down"));

        AdminOperationResult result = await service.TriggerBackupAsync("tenant-a", null, true);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldNotBeNull();
    }

    [Fact]
    public async Task TriggerBackupAsync_ReturnsNullResponseError_WhenEventStoreReturnsNull()
    {
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupNullJsonResponse();

        AdminOperationResult result = await service.TriggerBackupAsync("tenant-a", null, true);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NULL_RESPONSE");
    }

    [Fact]
    public async Task TriggerBackupAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.TriggerBackupAsync("tenant-a", null, true, cts.Token));
    }

    // === ValidateBackupAsync ===

    [Fact]
    public async Task ValidateBackupAsync_ReturnsSuccess_WhenBackupValid()
    {
        var expected = new AdminOperationResult(true, "op-2", "Backup valid", null);
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.ValidateBackupAsync("backup-123");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateBackupAsync_ReturnsError_WhenServiceUnavailable()
    {
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new HttpRequestException("Connection refused"));

        AdminOperationResult result = await service.ValidateBackupAsync("backup-123");

        result.Success.ShouldBeFalse();
    }

    // === TriggerRestoreAsync ===

    [Fact]
    public async Task TriggerRestoreAsync_ReturnsSuccess_WithPointInTimeAndDryRun()
    {
        var expected = new AdminOperationResult(true, "op-3", "Restore started", null);
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.TriggerRestoreAsync(
            "backup-123",
            DateTimeOffset.UtcNow.AddHours(-1),
            dryRun: true);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task TriggerRestoreAsync_ReturnsError_WhenServiceUnavailable()
    {
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("EventStore down"));

        AdminOperationResult result = await service.TriggerRestoreAsync("backup-123", null, false);

        result.Success.ShouldBeFalse();
    }

    // === ExportStreamAsync ===

    [Fact]
    public async Task ExportStreamAsync_ReturnsResult_WhenEventStoreResponds()
    {
        var expected = new StreamExportResult(true, "tenant-a", "Counter", "counter-1", 42, "{}", "export.json", null);
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        StreamExportResult result = await service.ExportStreamAsync(
            new StreamExportRequest("tenant-a", "Counter", "counter-1"));

        result.Success.ShouldBeTrue();
        result.EventCount.ShouldBe(42);
    }

    [Fact]
    public async Task ExportStreamAsync_ReturnsError_WhenEventStoreReturnsNull()
    {
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupNullJsonResponse();

        StreamExportResult result = await service.ExportStreamAsync(
            new StreamExportRequest("tenant-a", "Counter", "counter-1"));

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Null response");
    }

    [Fact]
    public async Task ExportStreamAsync_ReturnsError_WhenServiceUnavailable()
    {
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new HttpRequestException("Connection refused"));

        StreamExportResult result = await service.ExportStreamAsync(
            new StreamExportRequest("tenant-a", "Counter", "counter-1"));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExportStreamAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.ExportStreamAsync(
                new StreamExportRequest("tenant-a", "Counter", "counter-1"), cts.Token));
    }

    // === ImportStreamAsync ===

    [Fact]
    public async Task ImportStreamAsync_ReturnsSuccess_WhenEventStoreAccepts()
    {
        var expected = new AdminOperationResult(true, "op-4", "Import complete", null);
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(expected);

        AdminOperationResult result = await service.ImportStreamAsync("tenant-a", "{events:[]}");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ImportStreamAsync_ReturnsError_WhenServiceUnavailable()
    {
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("EventStore down"));

        AdminOperationResult result = await service.ImportStreamAsync("tenant-a", "{events:[]}");

        result.Success.ShouldBeFalse();
    }

    // === Error code extraction ===

    [Fact]
    public async Task InvokePost_ExtractsHttpStatusCode_FromHttpRequestException()
    {
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupErrorResponse(HttpStatusCode.NotFound);

        AdminOperationResult result = await service.TriggerBackupAsync("tenant-a", null, true);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("404");
    }
}
