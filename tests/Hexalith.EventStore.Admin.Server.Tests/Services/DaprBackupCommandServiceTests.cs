#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprBackupCommandServiceTests
{
    private const string EventStoreAppId = "eventstore";

    private static DaprBackupCommandService CreateService(
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

        return new DaprBackupCommandService(
            daprClient,
            options,
            authContext,
            NullLogger<DaprBackupCommandService>.Instance);
    }

    // === TriggerBackupAsync ===

    [Fact]
    public async Task TriggerBackupAsync_ReturnsSuccess_WhenEventStoreResponds()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-1", "Backup started", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprBackupCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.TriggerBackupAsync("tenant-a", "nightly", true);

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldBe("op-1");
    }

    [Fact]
    public async Task TriggerBackupAsync_ForwardsJwtToken()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        HttpRequestMessage? capturedRequest = null;
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        authContext.GetToken().Returns("backup-token");

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Do<HttpRequestMessage>(r => capturedRequest = r),
            Arg.Any<CancellationToken>())
            .Returns(_ => new AdminOperationResult(true, "op-1", null, null));

        DaprBackupCommandService service = CreateService(daprClient, authContext);

        await service.TriggerBackupAsync("tenant-a", null, false);

        capturedRequest.ShouldNotBeNull();
        capturedRequest!.Headers.Authorization!.Parameter.ShouldBe("backup-token");
    }

    [Fact]
    public async Task TriggerBackupAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("EventStore down"));

        DaprBackupCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.TriggerBackupAsync("tenant-a", null, true);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldNotBeNull();
    }

    [Fact]
    public async Task TriggerBackupAsync_ReturnsNullResponseError_WhenEventStoreReturnsNull()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => (AdminOperationResult?)null);

        DaprBackupCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.TriggerBackupAsync("tenant-a", null, true);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NULL_RESPONSE");
    }

    [Fact]
    public async Task TriggerBackupAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns<AdminOperationResult?>(_ => throw new OperationCanceledException());

        DaprBackupCommandService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.TriggerBackupAsync("tenant-a", null, true, cts.Token));
    }

    // === ValidateBackupAsync ===

    [Fact]
    public async Task ValidateBackupAsync_ReturnsSuccess_WhenBackupValid()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-2", "Backup valid", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprBackupCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.ValidateBackupAsync("backup-123");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateBackupAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        DaprBackupCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.ValidateBackupAsync("backup-123");

        result.Success.ShouldBeFalse();
    }

    // === TriggerRestoreAsync ===

    [Fact]
    public async Task TriggerRestoreAsync_ReturnsSuccess_WithPointInTimeAndDryRun()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-3", "Restore started", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprBackupCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.TriggerRestoreAsync(
            "backup-123",
            DateTimeOffset.UtcNow.AddHours(-1),
            dryRun: true);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task TriggerRestoreAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("EventStore down"));

        DaprBackupCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.TriggerRestoreAsync("backup-123", null, false);

        result.Success.ShouldBeFalse();
    }

    // === ExportStreamAsync ===

    [Fact]
    public async Task ExportStreamAsync_ReturnsResult_WhenEventStoreResponds()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new StreamExportResult(true, "tenant-a", "Counter", "counter-1", 42, "{}", "export.json", null);

        daprClient.InvokeMethodAsync<StreamExportResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprBackupCommandService service = CreateService(daprClient);

        StreamExportResult result = await service.ExportStreamAsync(
            new StreamExportRequest("tenant-a", "Counter", "counter-1"));

        result.Success.ShouldBeTrue();
        result.EventCount.ShouldBe(42);
    }

    [Fact]
    public async Task ExportStreamAsync_ReturnsError_WhenEventStoreReturnsNull()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<StreamExportResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => (StreamExportResult?)null);

        DaprBackupCommandService service = CreateService(daprClient);

        StreamExportResult result = await service.ExportStreamAsync(
            new StreamExportRequest("tenant-a", "Counter", "counter-1"));

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Null response");
    }

    [Fact]
    public async Task ExportStreamAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<StreamExportResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        DaprBackupCommandService service = CreateService(daprClient);

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

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<StreamExportResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns<StreamExportResult?>(_ => throw new OperationCanceledException());

        DaprBackupCommandService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.ExportStreamAsync(
                new StreamExportRequest("tenant-a", "Counter", "counter-1"), cts.Token));
    }

    // === ImportStreamAsync ===

    [Fact]
    public async Task ImportStreamAsync_ReturnsSuccess_WhenEventStoreAccepts()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var expected = new AdminOperationResult(true, "op-4", "Import complete", null);

        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => expected);

        DaprBackupCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.ImportStreamAsync("tenant-a", "{events:[]}");

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ImportStreamAsync_ReturnsError_WhenServiceUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("EventStore down"));

        DaprBackupCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.ImportStreamAsync("tenant-a", "{events:[]}");

        result.Success.ShouldBeFalse();
    }

    // === Error code extraction ===

    [Fact]
    public async Task InvokePost_ExtractsHttpStatusCode_FromHttpRequestException()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.InvokeMethodAsync<AdminOperationResult>(
            Arg.Any<HttpRequestMessage>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Not found", null, System.Net.HttpStatusCode.NotFound));

        DaprBackupCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.TriggerBackupAsync("tenant-a", null, true);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("404");
    }
}
