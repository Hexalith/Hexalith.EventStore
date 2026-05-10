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

public class DaprBackupCommandServiceTests {
    private const string EventStoreAppId = "eventstore";

    private static (DaprBackupCommandService Service, TestHttpMessageHandler Handler) CreateService() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            EventStoreAppId = EventStoreAppId,
            ServiceInvocationTimeoutSeconds = 30,
        });

        var handler = new TestHttpMessageHandler();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new DaprBackupCommandService(
            daprClient,
            httpClientFactory,
            options,
            new NullAdminAuthContext(),
            NullLogger<DaprBackupCommandService>.Instance);

        return (service, handler);
    }

    [Fact]
    public async Task TriggerBackupAsync_ReturnsDeferred_WithoutCallingEventStore() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();

        AdminOperationResult result = await service.TriggerBackupAsync("tenant-a", "nightly", true);

        result.Success.ShouldBeFalse();
        result.OperationId.ShouldBe("deferred-backup-trigger");
        result.ErrorCode.ShouldBe("Deferred");
        result.Message!.ShouldContain("Backup creation is deferred");
        handler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateBackupAsync_ReturnsDeferred_WithoutCallingEventStore() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();

        AdminOperationResult result = await service.ValidateBackupAsync("backup-123");

        result.Success.ShouldBeFalse();
        result.OperationId.ShouldBe("deferred-backup-validate");
        result.ErrorCode.ShouldBe("Deferred");
        result.Message!.ShouldContain("Backup validation is deferred");
        handler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task TriggerRestoreAsync_ReturnsDeferred_WithoutCallingEventStore() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();

        AdminOperationResult result = await service.TriggerRestoreAsync("backup-123", null, dryRun: true);

        result.Success.ShouldBeFalse();
        result.OperationId.ShouldBe("deferred-backup-restore");
        result.ErrorCode.ShouldBe("Deferred");
        result.Message!.ShouldContain("Restore is deferred");
        handler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExportStreamAsync_ReturnsDeferredResult_WithoutCallingEventStore() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();

        StreamExportResult result = await service.ExportStreamAsync(new StreamExportRequest("tenant-a", "Counter", "counter-1"));

        result.Success.ShouldBeFalse();
        result.EventCount.ShouldBe(0);
        result.ErrorMessage!.ShouldContain("Stream export is deferred");
        handler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task ImportStreamAsync_ReturnsDeferred_WithoutCallingEventStore() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler) = CreateService();

        AdminOperationResult result = await service.ImportStreamAsync("tenant-a", """{"Events":[]}""");

        result.Success.ShouldBeFalse();
        result.OperationId.ShouldBe("deferred-backup-import-stream");
        result.ErrorCode.ShouldBe("Deferred");
        result.Message!.ShouldContain("Stream import is deferred");
        handler.RequestCount.ShouldBe(0);
    }
}
