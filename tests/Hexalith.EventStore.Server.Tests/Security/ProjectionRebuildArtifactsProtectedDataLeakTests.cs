using System;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Testing.Security;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>
/// Story 22.7d-4 — regression scans proving rebuild/checkpoint DTOs and Admin status surfaces
/// expose only safe identifiers, operation IDs, status, and reason codes. Sentinel-bearing values
/// planted in the failure-reason field must serialize through the DTO without sentinel leakage —
/// the DTO contract does not include raw provider exception text fields, so any future regression
/// adding such a field will be caught by these scans.
/// </summary>
public class ProjectionRebuildArtifactsProtectedDataLeakTests {
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ProjectionRebuildCheckpoint_SerializedWithSafeReasonCode_ContainsNoSentinel() {
        var checkpoint = new ProjectionRebuildCheckpoint(
            Tenant: "tenant-a",
            Domain: "orders",
            ProjectionName: "orders-summary",
            AggregateId: null,
            OperationId: "01HZOPZZZZZZZZZZZZZZZZZZZZ",
            LastAppliedSequence: 42,
            Status: ProjectionRebuildStatus.Failed,
            UpdatedAt: DateTimeOffset.UnixEpoch,
            FailureReasonCode: UnreadableProtectedDataReasonCodes.MissingKey,
            ToPosition: 100);

        string json = JsonSerializer.Serialize(checkpoint, JsonOpts);

        ProtectedDataLeakSentinel.AssertNoLeak([
            json,
            checkpoint.FailureReasonCode,
            checkpoint.OperationId,
            checkpoint.Tenant,
            checkpoint.Domain,
            checkpoint.ProjectionName,
        ]);
        json.ShouldContain("\"failureReasonCode\":");
        json.ShouldContain(UnreadableProtectedDataReasonCodes.MissingKey);
    }

    [Fact]
    public void ProjectionRebuildOperation_SerializedWithFailedStatus_ContainsNoSentinel() {
        var operation = new ProjectionRebuildOperation(
            OperationId: "01HZOPZZZZZZZZZZZZZZZZZZZZ",
            Tenant: "tenant-a",
            Domain: "orders",
            ProjectionName: "orders-summary",
            AggregateId: null,
            Status: ProjectionRebuildStatus.Failed,
            Checkpoint: null,
            StartedAt: DateTimeOffset.UnixEpoch,
            CompletedAt: DateTimeOffset.UnixEpoch.AddSeconds(60),
            FailureReasonCode: UnreadableProtectedDataReasonCodes.ProviderUnavailable);

        string json = JsonSerializer.Serialize(operation, JsonOpts);
        ProtectedDataLeakSentinel.AssertNoLeak([
            json,
            operation.FailureReasonCode,
            operation.OperationId,
        ]);
        json.ShouldContain("\"failureReasonCode\":");
    }

    [Fact]
    public void ProjectionRebuildOperation_SerializedWithoutFailure_ContainsSafeMetadataOnly() {
        var operation = new ProjectionRebuildOperation(
            OperationId: "01HZOPZZZZZZZZZZZZZZZZZZZZ",
            Tenant: "tenant-a",
            Domain: "orders",
            ProjectionName: "orders-summary",
            AggregateId: "order-001",
            Status: ProjectionRebuildStatus.Running,
            Checkpoint: new ProjectionRebuildCheckpoint(
                Tenant: "tenant-a",
                Domain: "orders",
                ProjectionName: "orders-summary",
                AggregateId: "order-001",
                OperationId: "01HZOPZZZZZZZZZZZZZZZZZZZZ",
                LastAppliedSequence: 7,
                Status: ProjectionRebuildStatus.Running,
                UpdatedAt: DateTimeOffset.UnixEpoch,
                FailureReasonCode: null,
                ToPosition: null),
            StartedAt: DateTimeOffset.UnixEpoch,
            CompletedAt: null,
            FailureReasonCode: null);

        string json = JsonSerializer.Serialize(operation, JsonOpts);
        ProtectedDataLeakSentinel.AssertNoLeak([json]);
        // No raw payload, snapshot, or exception fields should be in scope.
        json.ShouldNotContain("payload", Case.Insensitive);
        json.ShouldNotContain("exception", Case.Insensitive);
        json.ShouldNotContain("stackTrace", Case.Insensitive);
    }

    [Fact]
    public void ProjectionRebuildCheckpointStore_SanitizeFailureReasonCode_ReplacesUnsafeText() {
        string? reasonCode = ProjectionRebuildCheckpointStore.SanitizeFailureReasonCode(
            $"provider-{ProtectedDataLeakSentinel.ProtectedProviderExceptionText}");

        reasonCode.ShouldBe(StreamReplayReasonCodes.InternalError);
        ProtectedDataLeakSentinel.AssertNoLeak([reasonCode]);
    }

    [Theory]
    [InlineData(StreamReplayReasonCodes.DomainFailure)]
    [InlineData(UnreadableProtectedDataReasonCodes.MissingKey)]
    [InlineData(UnreadableProtectedDataReasonCodes.ProviderUnavailable)]
    public void ProjectionRebuildCheckpointStore_SanitizeFailureReasonCode_PreservesStableCodes(string input) {
        string? reasonCode = ProjectionRebuildCheckpointStore.SanitizeFailureReasonCode(input);

        reasonCode.ShouldBe(input);
    }
}
