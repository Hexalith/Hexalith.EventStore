using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprBackupCommandServiceTests {
    private const string EventStoreAppId = "eventstore";

    private static (DaprBackupCommandService Service, TestHttpMessageHandler Handler, DaprClient DaprClient) CreateService(
        int maxStreamExportEvents = 50_000,
        IAdminAuthContext? authContext = null,
        ILogger<DaprBackupCommandService>? logger = null) {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            EventStoreAppId = EventStoreAppId,
            ServiceInvocationTimeoutSeconds = 30,
            MaxStreamExportEvents = maxStreamExportEvents,
        });

        var handler = new TestHttpMessageHandler();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new DaprBackupCommandService(
            daprClient,
            httpClientFactory,
            options,
            authContext ?? new NullAdminAuthContext(),
            logger ?? NullLogger<DaprBackupCommandService>.Instance);

        return (service, handler, daprClient);
    }

    [Fact]
    public async Task TriggerBackupAsync_ReturnsDeferred_WithoutCallingEventStore() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService();

        AdminOperationResult result = await service.TriggerBackupAsync("tenant-a", "nightly", true);

        result.Success.ShouldBeFalse();
        result.OperationId.ShouldBe("deferred-backup-trigger");
        result.ErrorCode.ShouldBe("Deferred");
        result.Message!.ShouldContain("Backup creation is deferred");
        handler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateBackupAsync_ReturnsDeferred_WithoutCallingEventStore() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService();

        AdminOperationResult result = await service.ValidateBackupAsync("backup-123");

        result.Success.ShouldBeFalse();
        result.OperationId.ShouldBe("deferred-backup-validate");
        result.ErrorCode.ShouldBe("Deferred");
        result.Message!.ShouldContain("Backup validation is deferred");
        handler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task TriggerRestoreAsync_ReturnsDeferred_WithoutCallingEventStore() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService();

        AdminOperationResult result = await service.TriggerRestoreAsync("backup-123", null, dryRun: true);

        result.Success.ShouldBeFalse();
        result.OperationId.ShouldBe("deferred-backup-restore");
        result.ErrorCode.ShouldBe("Deferred");
        result.Message!.ShouldContain("Restore is deferred");
        handler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExportStreamAsync_EmptyStreamReadReturnsNoExportableEvents() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService();
        handler.SetupJsonResponse(new StreamReadPage(
            "tenant-a",
            "counter",
            "counter-1",
            [],
            new StreamReadMetadata(0, null, null, 0, 0, false, null)));

        StreamExportResult result = await service.ExportStreamAsync(new StreamExportRequest("tenant-a", "counter", "counter-1"));

        result.Success.ShouldBeFalse();
        result.EventCount.ShouldBe(0);
        result.Content.ShouldBeNull();
        result.FileName.ShouldBeNull();
        result.ErrorCode.ShouldBe("NoExportableEvents");
        handler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task ExportStreamAsync_DW18AC1_ReadsEventStoreStreamAndReturnsJsonExport() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService();
        handler.SetupJsonResponse(new StreamReadPage(
            "tenant-a",
            "counter",
            "counter-1",
            [
                CreateStreamEvent(1, """{"value":1}"""),
                CreateStreamEvent(2, """{"value":2}"""),
            ],
            new StreamReadMetadata(0, null, 2, 2, 2, false, null)));

        StreamExportResult result = await service.ExportStreamAsync(new StreamExportRequest("tenant-a", "counter", "counter-1", "JSON"));

        result.Success.ShouldBeTrue();
        result.EventCount.ShouldBe(2);
        result.FileName.ShouldNotBeNull();
        result.FileName!.ShouldEndWith(".json");
        result.Content.ShouldNotBeNull();
        result.Content!.ShouldContain("\"format\":\"JSON\"");
        result.Content.ShouldContain("\"eventCount\":2");
        result.Content.ShouldContain("\"truncated\":false");
        result.Content.ShouldContain("\"payload\":{\"value\":1}");
        handler.RequestCount.ShouldBe(1);
        handler.LastRequestBody.ShouldNotBeNull();
        handler.LastRequestBody!.ShouldContain("\"tenant\":\"tenant-a\"");
        handler.LastRequestBody.ShouldContain("\"domain\":\"counter\"");
        handler.LastRequestBody.ShouldContain("\"aggregateId\":\"counter-1\"");
        handler.LastRequestBody.ShouldContain("\"pageSize\":1000");
    }

    [Fact]
    public async Task ExportStreamAsync_DW18AC3_UnsupportedFormatReturnsValidationFailureWithoutEventStoreCall() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService();

        StreamExportResult result = await service.ExportStreamAsync(new StreamExportRequest("tenant-a", "counter", "counter-1", "XML"));

        result.Success.ShouldBeFalse();
        result.Content.ShouldBeNull();
        result.FileName.ShouldBeNull();
        result.ErrorCode.ShouldBe("RejectedValidation");
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage!.ShouldContain("Unsupported stream export format");
        handler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExportStreamAsync_DW18AC4_ReturnsCloudEventsDocument() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService();
        handler.SetupJsonResponse(new StreamReadPage(
            "tenant-a",
            "counter",
            "counter-1",
            [CreateStreamEvent(1, """{"value":1}""")],
            new StreamReadMetadata(0, null, 1, 1, 1, false, null)));

        StreamExportResult result = await service.ExportStreamAsync(new StreamExportRequest("tenant-a", "counter", "counter-1", "CloudEvents"));

        result.Success.ShouldBeTrue();
        result.Content.ShouldNotBeNull();
        result.Content!.ShouldContain("\"format\":\"CloudEvents\"");
        result.Content.ShouldContain("\"specversion\":\"1.0\"");
        result.Content.ShouldContain("\"source\":\"/eventstore/tenants/tenant-a/domains/counter/aggregates/counter-1\"");
        result.Content.ShouldContain("\"data\":{\"value\":1}");
    }

    [Fact]
    public async Task ExportStreamAsync_DW18AC2_OversizedStreamExportsNewestWindow() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService(maxStreamExportEvents: 3);
        handler.SetupResponseSequence(
            System.Net.Http.Json.JsonContent.Create(new StreamReadPage(
                "tenant-a",
                "counter",
                "counter-1",
                [CreateStreamEvent(1, """{"value":1}""")],
                new StreamReadMetadata(0, null, 1, 5, 1, true, null))).ReadAsResponse(),
            System.Net.Http.Json.JsonContent.Create(new StreamReadPage(
                "tenant-a",
                "counter",
                "counter-1",
                [
                    CreateStreamEvent(3, """{"value":3}"""),
                    CreateStreamEvent(4, """{"value":4}"""),
                    CreateStreamEvent(5, """{"value":5}"""),
                ],
                new StreamReadMetadata(2, 5, 5, 5, 3, false, null))).ReadAsResponse());

        StreamExportResult result = await service.ExportStreamAsync(new StreamExportRequest("tenant-a", "counter", "counter-1", "JSON"));

        result.Success.ShouldBeTrue();
        result.EventCount.ShouldBe(3);
        result.Content.ShouldNotBeNull();
        result.Content!.ShouldContain("\"fromSequence\":3");
        result.Content.ShouldContain("\"toSequence\":5");
        result.Content.ShouldContain("\"exportLimit\":3");
        result.Content.ShouldContain("\"truncated\":true");
        result.Content.ShouldNotContain("\"sequenceNumber\":1");
        result.Content.ShouldContain("\"sequenceNumber\":3");
        handler.RequestCount.ShouldBe(2);
        handler.LastRequestBody.ShouldNotBeNull();
        handler.LastRequestBody!.ShouldContain("\"fromSequence\":2");
        handler.LastRequestBody.ShouldContain("\"toSequence\":5");
    }

    [Fact]
    public async Task ExportStreamAsync_DW18AC5_MissingStreamProblemReturnsSafeFailure() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService();
        handler.SetupJsonResponse(new ProblemDetails {
            Status = 404,
            Title = "Not Found",
            Detail = "Event stream does not exist.",
            Extensions = { ["reasonCode"] = StreamReplayReasonCodes.MissingStream },
        }, System.Net.HttpStatusCode.NotFound);

        StreamExportResult result = await service.ExportStreamAsync(new StreamExportRequest("tenant-a", "counter", "counter-1", "JSON"));

        result.Success.ShouldBeFalse();
        result.Content.ShouldBeNull();
        result.FileName.ShouldBeNull();
        result.ErrorCode.ShouldBe(StreamReplayReasonCodes.MissingStream);
        result.ErrorMessage.ShouldBe("Stream export failed. ReasonCode=missing-stream.");
    }

    [Fact]
    public async Task ExportStreamAsync_DW18AC5_NonProblemDetailsUnauthorizedMapsStableFailure() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService();
        handler.SetupResponse(new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized) {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain"),
        });

        StreamExportResult result = await service.ExportStreamAsync(new StreamExportRequest("tenant-a", "counter", "counter-1", "JSON"));

        result.Success.ShouldBeFalse();
        result.Content.ShouldBeNull();
        result.FileName.ShouldBeNull();
        result.ErrorCode.ShouldBe(StreamReplayReasonCodes.UnauthorizedTenant);
        result.ErrorMessage.ShouldBe("Stream export failed. ReasonCode=unauthorized-tenant.");
    }

    [Fact]
    public async Task ExportStreamAsync_DW18AC5_TransportFailureMapsToSafeUnavailableResult() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService();
        handler.SetupException(new HttpRequestException("connection string sentinel must not leak"));

        StreamExportResult result = await service.ExportStreamAsync(new StreamExportRequest("tenant-a", "counter", "counter-1", "JSON"));

        result.Success.ShouldBeFalse();
        result.Content.ShouldBeNull();
        result.FileName.ShouldBeNull();
        result.ErrorCode.ShouldBe(StreamReplayReasonCodes.ServiceUnavailable);
        result.ErrorMessage.ShouldBe("Stream export failed. ReasonCode=service-unavailable.");
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldNotContain("connection string");
    }

    [Fact]
    public async Task ExportStreamAsync_DW18AC5_ProtectedPayloadFailsClosedWithoutContent() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService();
        handler.SetupJsonResponse(new StreamReadPage(
            "tenant-a",
            "counter",
            "counter-1",
            [CreateStreamEvent(1, """{"value":1}""") with { ProtectionMetadata = EventStorePayloadProtectionMetadata.ProviderOpaque("sentinel") }],
            new StreamReadMetadata(0, null, 1, 1, 1, false, null)));

        StreamExportResult result = await service.ExportStreamAsync(new StreamExportRequest("tenant-a", "counter", "counter-1", "JSON"));

        result.Success.ShouldBeFalse();
        result.Content.ShouldBeNull();
        result.FileName.ShouldBeNull();
        result.ErrorCode.ShouldBe(StreamReplayReasonCodes.ProtectedPayloadUnavailable);
        result.ErrorMessage.ShouldBe("Stream export failed. ReasonCode=protected-payload-unavailable.");
    }

    [Fact]
    public async Task ExportStreamAsync_DW18AC7_LogsSafeAuditFieldsForSuccess() {
        var logger = new RecordingLogger<DaprBackupCommandService>();
        var authContext = new TestAdminAuthContext("operator-sub", "corr-123");
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService(
            maxStreamExportEvents: 50_000,
            authContext,
            logger);
        handler.SetupJsonResponse(new StreamReadPage(
            "tenant-a",
            "counter",
            "counter-1",
            [CreateStreamEvent(1, """{"value":1}""")],
            new StreamReadMetadata(0, null, 1, 1, 1, false, null)));

        StreamExportResult result = await service.ExportStreamAsync(new StreamExportRequest("tenant-a", "counter", "counter-1", "JSON"));

        result.Success.ShouldBeTrue();
        RecordingLogger<DaprBackupCommandService>.LogRecord record = logger.Records.ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Information);
        record.Message.ShouldContain("SubjectId=operator-sub");
        record.Message.ShouldContain("CorrelationId=corr-123");
        record.Message.ShouldContain("RequestedCap=50000");
        record.Message.ShouldContain("EventCount=1");
        record.Message.ShouldContain("Truncated=False");
    }

    [Fact]
    public async Task ExportStreamAsync_DW18AC7_LogsSafeAuditFieldsForValidationFailure() {
        var logger = new RecordingLogger<DaprBackupCommandService>();
        var authContext = new TestAdminAuthContext("operator-sub", "corr-456");
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService(
            authContext: authContext,
            logger: logger);

        StreamExportResult result = await service.ExportStreamAsync(new StreamExportRequest("tenant-a", "counter", "counter-1", "XML"));

        result.Success.ShouldBeFalse();
        handler.RequestCount.ShouldBe(0);
        RecordingLogger<DaprBackupCommandService>.LogRecord record = logger.Records.ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldContain("SubjectId=operator-sub");
        record.Message.ShouldContain("CorrelationId=corr-456");
        record.Message.ShouldContain("RequestedCap=50000");
        record.Message.ShouldContain("ReasonCode=RejectedValidation");
        record.Message.ShouldContain("Format=XML");
    }


    [Fact]
    public async Task ExportStreamAsync_InvalidMaxEventsConfigurationFailsBeforeEventStoreCall() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService(maxStreamExportEvents: 0);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => service.ExportStreamAsync(new StreamExportRequest("tenant-a", "counter", "counter-1", "JSON")));

        exception.Message.ShouldContain("MaxStreamExportEvents");
        handler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task ImportStreamAsync_ReturnsDeferred_WithoutCallingEventStore() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService();

        AdminOperationResult result = await service.ImportStreamAsync("tenant-a", """{"Events":[]}""");

        result.Success.ShouldBeFalse();
        result.OperationId.ShouldBe("deferred-backup-import-stream");
        result.ErrorCode.ShouldBe("Deferred");
        result.Message!.ShouldContain("Stream import is deferred");
        handler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task AdmitRestoredBackupAsync_ValidRequest_ReturnsDeferredValidation() {
        (DaprBackupCommandService service, _, DaprClient daprClient) = CreateService();
        var request = new RestoredBackupAdmissionRequest(
            AdmissionId: "01HKADADADADADADADADADADAD",
            TenantId: "tenant-a",
            Domain: "orders",
            AggregateId: "agg-1",
            FromSequence: 0,
            ToSequence: 100,
            BackupManifestId: "manifest-1",
            BackupCreatedAtUtc: DateTimeOffset.UtcNow,
            RestoreRequestedAtUtc: DateTimeOffset.UtcNow,
            ProtectionMetadataVersion: 1,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null,
            DeletionWatermarkUtc: null,
            CorrelationId: "01HKCORR",
            OperatorActorId: "operator");

        RestoredBackupAdmissionResult result = await service.AdmitRestoredBackupAsync(request);

        result.State.ShouldBe(RestoredBackupAdmissionState.DeferredValidation);
        result.ReasonCode.ShouldBe(RestoredBackupAdmissionResult.DeferredValidationCode);
        result.WatermarkConflict.ShouldBe("backup-engine-deferred");
        result.NextAction.ShouldBe(CryptoShreddingNextAction.ProvideRestoreEvidence);
        result.AdmissionId.ShouldBe(request.AdmissionId);
        result.TenantId.ShouldBe(request.TenantId);
        await daprClient.Received(1).SaveStateAsync(
            "statestore",
            DaprBackupCommandService.AdmissionKey(request.TenantId, request.AdmissionId),
            Arg.Is<RestoredBackupAdmissionResult>(r => r.AdmissionId == request.AdmissionId),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdmitRestoredBackupAsync_InvalidRequest_Throws() {
        (DaprBackupCommandService service, _, _) = CreateService();
        // Invalid: range scope without sequences (FromSequence > ToSequence).
        var request = new RestoredBackupAdmissionRequest(
            AdmissionId: "01HKADADADADADADADADADADAD",
            TenantId: "tenant-a",
            Domain: "orders",
            AggregateId: "agg-1",
            FromSequence: 200,
            ToSequence: 100,
            BackupManifestId: "manifest-1",
            BackupCreatedAtUtc: DateTimeOffset.UtcNow,
            RestoreRequestedAtUtc: DateTimeOffset.UtcNow,
            ProtectionMetadataVersion: 1,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null,
            DeletionWatermarkUtc: null,
            CorrelationId: "01HKCORR",
            OperatorActorId: "operator");

        _ = await Should.ThrowAsync<ArgumentException>(() => service.AdmitRestoredBackupAsync(request));
    }

    [Fact]
    public async Task SubmitRestoreAdmissionDecisionAsync_StoredDeferredAdmission_AcceptsDecision() {
        (DaprBackupCommandService service, _, DaprClient daprClient) = CreateService();
        var existing = new RestoredBackupAdmissionResult(
            AdmissionId: "01HKADADADADADADADADADADAD",
            State: RestoredBackupAdmissionState.DeferredValidation,
            TenantId: "tenant-a",
            Domain: "orders",
            AggregateId: "agg-1",
            FromSequence: 0,
            ToSequence: 100,
            BackupManifestId: "manifest-1",
            ProtectionMetadataVersion: 1,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null,
            WatermarkConflict: "backup-engine-deferred",
            ReasonCode: RestoredBackupAdmissionResult.DeferredValidationCode,
            NextAction: CryptoShreddingNextAction.ProvideRestoreEvidence,
            CorrelationId: "01HKCORR",
            AuditId: null,
            DecisionActorId: "operator",
            DecidedAtUtc: DateTimeOffset.UtcNow,
            IdempotentReplay: false);
        _ = daprClient.GetStateAsync<RestoredBackupAdmissionResult>(
            "statestore",
            DaprBackupCommandService.AdmissionKey("tenant-a", existing.AdmissionId),
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(existing);

        RestoredBackupAdmissionResult result = await service.SubmitRestoreAdmissionDecisionAsync(
            "tenant-a",
            "01HKADADADADADADADADADADAD",
            RestoredBackupAdmissionState.Accepted,
            "operator");

        result.State.ShouldBe(RestoredBackupAdmissionState.Accepted);
        result.AdmissionId.ShouldBe("01HKADADADADADADADADADADAD");
        result.DecisionActorId.ShouldBe("operator");
        result.AuditId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SubmitCryptoShreddingWorkflowAsync_RepeatedScope_ReturnsExistingDecision() {
        (DaprBackupCommandService service, _, DaprClient daprClient) = CreateService();
        CryptoShreddingWorkflowRequest request = new Hexalith.EventStore.Testing.Builders.CryptoShreddingWorkflowBuilder()
            .WithWorkflowId("01HKAAAAAAAAAAAAAAAAAAAAAA")
            .BuildRequest();
        CryptoShreddingWorkflowDecision existing = new Hexalith.EventStore.Testing.Builders.CryptoShreddingWorkflowBuilder()
            .WithWorkflowId("01HKBBBBBBBBBBBBBBBBBBBBBB")
            .BuildDecision();
        _ = daprClient.GetStateAsync<CryptoShreddingWorkflowDecision>(
            "statestore",
            Arg.Is<string>(key => key.StartsWith("admin:crypto-shredding-workflows:scope:", StringComparison.Ordinal)),
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(existing);

        CryptoShreddingWorkflowDecision result = await service.SubmitCryptoShreddingWorkflowAsync(request);

        result.IdempotentReplay.ShouldBeTrue();
        result.Identity.WorkflowId.ShouldBe(existing.Identity.WorkflowId);
    }

    private static StreamReadEvent CreateStreamEvent(long sequenceNumber, string payload)
        => new(
            sequenceNumber,
            "counter-incremented-v1",
            System.Text.Encoding.UTF8.GetBytes(payload),
            "json",
            1,
            $"01J{sequenceNumber:00000000000000000000000}",
            $"01C{sequenceNumber:00000000000000000000000}",
            $"01K{sequenceNumber:00000000000000000000000}",
            DateTimeOffset.Parse("2026-05-21T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            "operator-subject");

    private sealed class TestAdminAuthContext(string? userId, string? correlationId) : IAdminAuthContext {
        public string? GetToken() => null;

        public string? GetUserId() => userId;

        public string? GetCorrelationId() => correlationId;
    }
}

internal static class JsonContentTestExtensions {
    public static HttpResponseMessage ReadAsResponse(this HttpContent content)
        => new(System.Net.HttpStatusCode.OK) {
            Content = content,
        };
}
