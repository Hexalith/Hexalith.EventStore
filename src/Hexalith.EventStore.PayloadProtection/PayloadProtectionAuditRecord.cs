// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed PayloadProtectionAuditRecord durable schema; encodings are checked by the state codec.</summary>
/// <param name="AuditId">The canonical auditId field.</param>
/// <param name="OperationId">The canonical operationId field.</param>
/// <param name="WorkflowId">The canonical workflowId field.</param>
/// <param name="CorrelationId">The canonical correlationId field.</param>
/// <param name="TenantId">The canonical tenantId field.</param>
/// <param name="Domain">The canonical domain field.</param>
/// <param name="AggregateId">The canonical aggregateId field.</param>
/// <param name="Operation">The canonical operation field.</param>
/// <param name="FromState">The canonical fromState field.</param>
/// <param name="ToState">The canonical toState field.</param>
/// <param name="LifecycleEpoch">The canonical lifecycleEpoch field.</param>
/// <param name="FenceApprovalEpoch">The canonical fenceApprovalEpoch field.</param>
/// <param name="BackendId">The canonical backendId field.</param>
/// <param name="FormatVersion">The canonical formatVersion field.</param>
/// <param name="KeyFingerprint">The canonical keyFingerprint field.</param>
/// <param name="ProviderFingerprint">The canonical providerFingerprint field.</param>
/// <param name="AttemptCount">The canonical attemptCount field.</param>
/// <param name="DurationMilliseconds">The canonical durationMilliseconds field.</param>
/// <param name="Result">The canonical result field.</param>
/// <param name="ReasonCode">The canonical reasonCode field.</param>
/// <param name="DecisionActorId">The canonical decisionActorId field.</param>
/// <param name="DecidedAtUtc">The canonical decidedAtUtc field.</param>
internal sealed record PayloadProtectionAuditRecord(
    string AuditId,
    string OperationId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WorkflowId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CorrelationId,
    string TenantId,
    string Domain,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AggregateId,
    string Operation,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FromState,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ToState,
    ulong LifecycleEpoch,
    ulong FenceApprovalEpoch,
    string BackendId,
    string FormatVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? KeyFingerprint,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ProviderFingerprint,
    uint AttemptCount,
    ulong DurationMilliseconds,
    string Result,
    string ReasonCode,
    string DecisionActorId,
    string DecidedAtUtc)
{
    /// <summary>Gets the closed durable schema version.</summary>
    [JsonPropertyOrder(-1)]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(PayloadProtectionAuditRecord);
}
