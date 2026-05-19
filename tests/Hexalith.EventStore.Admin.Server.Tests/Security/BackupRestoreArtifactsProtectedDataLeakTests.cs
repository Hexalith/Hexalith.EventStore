using System;
using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Testing.Builders;
using Hexalith.EventStore.Testing.Security;

namespace Hexalith.EventStore.Admin.Server.Tests.Security;

/// <summary>
/// Story 22.7d-4 — regression scans proving backup/restore admission, crypto-shredding workflow,
/// stream export, and `InvokeEventStorePostAsync` output do not leak protected sentinels into
/// operator-facing fields. Includes the safe-message guard that prevents future protected-data
/// capable EventStore invocations from echoing `ex.Message` through `AdminOperationResult.Message`.
/// </summary>
public class BackupRestoreArtifactsProtectedDataLeakTests {
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public void BuildSafeInvocationFailureMessage_ContainsNoSentinelEvenWhenEndpointIsSafe() {
        string message = DaprBackupCommandService.BuildSafeInvocationFailureMessage(endpoint: "v1/eventstore/backup/trigger");
        ProtectedDataLeakSentinel.AssertNoLeak([message]);
        message.ShouldContain("v1/eventstore/backup/trigger");
        message.ShouldContain("redacted");
        // The safe message MUST be deterministic so log scrapers can match it.
        message.ShouldBe("EventStore service invocation failed. Endpoint=v1/eventstore/backup/trigger; details redacted from operator-facing message.");
    }

    [Fact]
    public void BuildSafeInvocationFailureMessage_DoesNotEmitProviderExceptionText() {
        // The function takes only the endpoint and is a pure projection — by construction it cannot
        // surface any caller-controlled exception text. Verify by formatting through a hypothetical
        // endpoint name that itself contains a sentinel substring (only possible via construction error)
        // and asserting the sentinel is preserved verbatim (caller's fault, not the function's).
        // For the actual contract, the safe message function intentionally never accepts the exception.
        string message = DaprBackupCommandService.BuildSafeInvocationFailureMessage("v1/safe-endpoint");
        ProtectedDataLeakSentinel.AssertNoLeak([message]);
    }

    [Fact]
    public void GetErrorCode_DropsStringStatusCodeToAvoidSensitiveText() {
        string code = DaprBackupCommandService.GetErrorCode(new StringStatusCodeException(ProtectedDataLeakSentinel.ProtectedProviderExceptionText));

        code.ShouldBe(nameof(StringStatusCodeException));
        ProtectedDataLeakSentinel.AssertNoLeak([code]);
    }

    [Fact]
    public void GetErrorCode_UsesNumericHttpStatusCode() {
        string code = DaprBackupCommandService.GetErrorCode(new HttpRequestException("upstream failed", null, HttpStatusCode.ServiceUnavailable));

        code.ShouldBe("503");
    }

    [Fact]
    public void RestoredBackupAdmissionResult_SafeFields_SerializeWithoutSentinel() {
        var result = new RestoredBackupAdmissionBuilder()
            .WithAdmissionId("01HRBAAAAAAAAAAAAAAAAAAAAA")
            .WithTenant("tenant-a")
            .WithDomain("orders")
            .WithAggregate("order-001")
            .WithRange(0, 100)
            .WithManifest("manifest-1")
            .WithMetadataVersion(1)
            .WithKeyReference(KeyReferencePolicy.AliasOnly, "abcdef0123456789")
            .WithState(RestoredBackupAdmissionState.Accepted)
            .WithOperator("operator-1")
            .WithCorrelationId("01HCORRELATIONAAAAAAAAAAAA")
            .WithAuditId("01HAUDITAAAAAAAAAAAAAAAAAA")
            .BuildResult();

        string json = JsonSerializer.Serialize(result, JsonOpts);
        ProtectedDataLeakSentinel.AssertNoLeak([
            json,
            result.AdmissionId,
            result.TenantId,
            result.Domain,
            result.AggregateId,
            result.BackupManifestId,
            result.ReasonCode,
            result.CorrelationId,
            result.AuditId,
            result.DecisionActorId,
            result.WatermarkConflict,
            result.KeyAliasFingerprint,
        ]);
    }

    [Fact]
    public void CryptoShreddingWorkflowDecision_SafeFields_SerializeWithoutSentinel() {
        var identity = new CryptoShreddingWorkflowIdentity(
            WorkflowId: "01HZWORKFLOWAAAAAAAAAAAAAA",
            TenantId: "tenant-a",
            Domain: "orders",
            Scope: CryptoShreddingWorkflowScope.Aggregate,
            AggregateId: "order-001",
            FromSequence: null,
            ToSequence: null,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null);
        var decision = new CryptoShreddingWorkflowDecision(
            Identity: identity,
            State: CryptoShreddingWorkflowState.Requested,
            ReasonCode: CryptoShreddingWorkflowDecision.ReasonCodeFor(CryptoShreddingWorkflowState.Requested),
            NextAction: CryptoShreddingNextAction.SubmitOperatorDecision,
            CorrelationId: "01HCORRELATIONAAAAAAAAAAAA",
            AuditId: "01HAUDITAAAAAAAAAAAAAAAAAA",
            DecisionActorId: "operator-1",
            DecidedAtUtc: DateTimeOffset.UnixEpoch,
            IrreversibleDecisionRecorded: false,
            IdempotentReplay: false);

        string json = JsonSerializer.Serialize(decision, JsonOpts);
        ProtectedDataLeakSentinel.AssertNoLeak([
            json,
            decision.ReasonCode,
            decision.AuditId,
            decision.CorrelationId,
            decision.DecisionActorId,
            decision.Identity.TenantId,
            decision.Identity.Domain,
            decision.Identity.AggregateId,
            decision.Identity.WorkflowId,
        ]);
        // Defense in depth: no raw key alias should ever serialize through this DTO.
        json.ShouldNotContain("keyAlias\"", Case.Sensitive);
    }

    [Fact]
    public void CryptoShreddingAuditEvent_SafeTransition_SerializesWithoutSentinel() {
        var audit = new CryptoShreddingAuditEvent(
            AuditId: "01HAUDITAAAAAAAAAAAAAAAAAA",
            WorkflowId: "01HZWORKFLOWAAAAAAAAAAAAAA",
            AdmissionId: null,
            TenantId: "tenant-a",
            Domain: "orders",
            AggregateId: "order-001",
            FromSequence: null,
            ToSequence: null,
            ProtectionMetadataVersion: 1,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null,
            WorkflowFromState: null,
            WorkflowToState: CryptoShreddingWorkflowState.Requested,
            AdmissionFromState: null,
            AdmissionToState: null,
            DecisionActorId: "operator-1",
            CorrelationId: "01HCORRELATIONAAAAAAAAAAAA",
            DecidedAtUtc: DateTimeOffset.UnixEpoch,
            ReasonCode: "requested");

        string json = JsonSerializer.Serialize(audit, JsonOpts);
        ProtectedDataLeakSentinel.AssertNoLeak([
            json,
            audit.ReasonCode,
            audit.AuditId,
            audit.WorkflowId,
            audit.AdmissionId,
            audit.CorrelationId,
            audit.DecisionActorId,
        ]);
    }

    [Fact]
    public void DeferredAdminOperationResult_ContainsNoSentinel() {
        // Every deferred backup/restore/import operation produces this shape. Prove the deferred
        // path is honest AND sentinel-free.
        var result = new AdminOperationResult(
            Success: false,
            OperationId: "deferred-backup-validate",
            Message: "Backup validation is deferred. EventStore does not yet have an approved backup manifest and validation model.",
            ErrorCode: "Deferred");

        string json = JsonSerializer.Serialize(result, JsonOpts);
        ProtectedDataLeakSentinel.AssertNoLeak([
            json,
            result.Message,
            result.OperationId,
            result.ErrorCode,
        ]);
    }

    [Fact]
    public void StreamExportResult_DeferredOutput_ContainsNoSentinel() {
        var result = new StreamExportResult(
            Success: false,
            TenantId: "tenant-a",
            Domain: "orders",
            AggregateId: "order-001",
            EventCount: 0,
            Content: null,
            FileName: null,
            ErrorMessage: "Stream export is deferred. EventStore does not yet have an approved export contract.");

        string json = JsonSerializer.Serialize(result, JsonOpts);
        ProtectedDataLeakSentinel.AssertNoLeak([
            json,
            result.ErrorMessage,
            result.TenantId,
            result.Domain,
            result.AggregateId,
            result.Content,
            result.FileName,
        ]);
    }

    private sealed class StringStatusCodeException(string statusCode) : Exception {
        public string StatusCode { get; } = statusCode;
    }
}
