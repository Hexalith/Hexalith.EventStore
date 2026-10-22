using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Testing.Builders;

namespace Hexalith.EventStore.Testing.Tests.Builders;

/// <summary>
/// Story 22.7c — Testing builder tests for crypto-shredding workflow and restored-backup admission
/// fixtures.
/// </summary>
public class CryptoShreddingBuildersTests {
    [Fact]
    public void Workflow_Builder_Defaults_BuildValidIdentity() {
        CryptoShreddingWorkflowIdentity id = new CryptoShreddingWorkflowBuilder().BuildIdentity();

        Assert.True(id.TryValidate(out string? reason), reason ?? "should be valid");
    }

    [Fact]
    public void Workflow_Builder_BuildRequest_AcceptsValidShape() {
        CryptoShreddingWorkflowRequest request = new CryptoShreddingWorkflowBuilder()
            .WithRequestedAction(CryptoShreddingWorkflowState.Deleted)
            .BuildRequest();

        Assert.True(request.TryValidate(out string? reason), reason ?? "should be valid");
        Assert.Equal(CryptoShreddingWorkflowState.Deleted, request.RequestedAction);
    }

    [Fact]
    public void Workflow_Builder_BuildDecision_ReportsIrreversibleForTerminalShredding() {
        CryptoShreddingWorkflowDecision decision = new CryptoShreddingWorkflowBuilder()
            .WithState(CryptoShreddingWorkflowState.Invalidated)
            .BuildDecision();

        Assert.Equal(CryptoShreddingWorkflowState.Invalidated, decision.State);
        Assert.True(decision.IrreversibleDecisionRecorded);
        Assert.Equal(
            CryptoShreddingWorkflowDecision.ReasonCodeFor(CryptoShreddingWorkflowState.Invalidated),
            decision.ReasonCode);
    }

    [Fact]
    public void Workflow_Builder_WithRange_SwitchesScope() {
        CryptoShreddingWorkflowIdentity id = new CryptoShreddingWorkflowBuilder()
            .WithRange(10, 20)
            .BuildIdentity();

        Assert.Equal(CryptoShreddingWorkflowScope.Range, id.Scope);
        Assert.Equal(10, id.FromSequence);
        Assert.Equal(20, id.ToSequence);
        Assert.True(id.TryValidate(out _));
    }

    [Fact]
    public void Restore_Admission_Builder_Defaults_BuildValidRequest() {
        RestoredBackupAdmissionRequest request = new RestoredBackupAdmissionBuilder().BuildRequest();

        Assert.True(request.TryValidate(out string? reason), reason ?? "should be valid");
    }

    [Fact]
    public void Restore_Admission_Builder_BuildResult_DefaultsToDeferredValidation() {
        RestoredBackupAdmissionResult result = new RestoredBackupAdmissionBuilder().BuildResult();

        Assert.Equal(RestoredBackupAdmissionState.DeferredValidation, result.State);
        Assert.Equal(RestoredBackupAdmissionResult.DeferredValidationCode, result.ReasonCode);
        Assert.Equal(CryptoShreddingNextAction.ProvideRestoreEvidence, result.NextAction);
    }

    [Fact]
    public void Restore_Admission_Builder_WithBlockedState_ProducesRestoreConflictDecision() {
        RestoredBackupAdmissionResult result = new RestoredBackupAdmissionBuilder()
            .WithState(RestoredBackupAdmissionState.Blocked)
            .WithWatermarkConflict("backup-before-deletion")
            .BuildResult();

        ProtectedDataReadabilityDecision decision = result.ToReadabilityDecision(
            ProtectedDataDecisionStage.Replay,
            metadataVersion: 1);

        Assert.Equal(ProtectedDataReadabilityStatus.RestoreConflict, decision.Status);
        Assert.False(decision.IsReadable);
        Assert.True(decision.IsPermanent);
    }
}
