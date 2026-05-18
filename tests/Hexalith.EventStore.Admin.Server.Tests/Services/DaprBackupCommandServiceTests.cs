using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;
using Hexalith.EventStore.Contracts.Security;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprBackupCommandServiceTests {
    private const string EventStoreAppId = "eventstore";

    private static (DaprBackupCommandService Service, TestHttpMessageHandler Handler, DaprClient DaprClient) CreateService() {
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
    public async Task ExportStreamAsync_ReturnsDeferredResult_WithoutCallingEventStore() {
        (DaprBackupCommandService service, TestHttpMessageHandler handler, _) = CreateService();

        StreamExportResult result = await service.ExportStreamAsync(new StreamExportRequest("tenant-a", "Counter", "counter-1"));

        result.Success.ShouldBeFalse();
        result.EventCount.ShouldBe(0);
        result.ErrorMessage!.ShouldContain("Stream export is deferred");
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
}
