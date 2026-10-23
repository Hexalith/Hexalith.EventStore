// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using System.Collections.Frozen;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Defines exact closed durable member names and semantic field profiles.</summary>
internal static class PayloadProtectionStateSchema
{
    /// <summary>Gets every enrolled schema by exact CLR type.</summary>
    internal static readonly FrozenDictionary<Type, string> Fields = new Dictionary<Type, string>
    {
        [typeof(WrappedDekV1)] = "tenantId:tenant domain:domain aggregateId:aggregate scopeDigest:sha keyRef:ulid dekVersion:positive state:Reserved|Active|RewrapPending|Invalidating|Invalidated|Deleted backendId:id providerKeyVersionId:provider wrapAlgorithmId:RSA-OAEP-256 wrappedDek:binary lifecycleEpoch:u64 fenceApprovalEpoch:u64 highestWrittenEpoch:u64 creatingOperationId:ulid createdAtUtc:timestamp updatedAtUtc:timestamp canonicalFieldsSha256:sha wrappedDekSha256:sha",
        [typeof(ScopeIndexHeadV1)] = "scopeDigest:sha generation:u64 pageCount:u32 entryCount:u32 updatedAtUtc:timestamp",
        [typeof(ScopeIndexPageV1)] = "scopeDigest:sha generation:u64 page:u32 entries:[ScopeIndexEntryV1]",
        [typeof(ScopeIndexEntryV1)] = "keyRef:ulid dekVersion:positive providerVersionDigest:sha state:Reserved|Active|RewrapPending|Invalidating|Invalidated|Deleted",
        [typeof(KekInventoryHeadV1)] = "backendIdDigest:sha providerVersionDigest:sha generation:u64 pageCount:u32 entryCount:u64 updatedAtUtc:timestamp",
        [typeof(KekInventoryPageV1)] = "backendIdDigest:sha providerVersionDigest:sha generation:u64 page:u32 entries:[KekInventoryEntryV1]",
        [typeof(KekInventoryEntryV1)] = "scopeDigest:sha keyRef:ulid dekVersion:positive",
        [typeof(LifecycleV1)] = "tenantId:tenant domain:domain aggregateId:aggregate scopeDigest:sha state:Active|Pending|Invalidating|Invalidated|Deleted epoch:u64 reasonCode:erasure operationId:ulid observedAtUtc:timestamp",
        [typeof(FenceV1)] = "tenantId:tenant domain:domain tenantDomainDigest:sha approvedWriteMode:Disabled|ReadOnly|CompatibilityLegacy|V2|RollbackLegacy approvalEpoch:u64 approvalId:ulid approvedSpecDigest:sha deploymentGeneration:u64 highestWrittenFormat:Legacy|V1|V2 highestWrittenEpoch:u64 updatedAtUtc:timestamp",
        [typeof(DeploymentGenerationV1)] = "tenantId:tenant domain:domain tenantDomainDigest:sha generation:u64 approvedSpecDigest:sha approvedSourceSha:sha1 packageSetSha256:sha status:Open|Closed approvedAtUtc:timestamp",
        [typeof(CapabilityLeaseV1)] = "tenantDomainDigest:sha generation:u64 instanceId:ulid sourceSha:sha1 packageSetSha256:sha specDigest:sha backendId:id readFormats:[format] writeModes:[mode] issuedAtUtc:timestamp expiresAtUtc:timestamp",
        [typeof(OperationV1)] = "operationId:ulid operation:Create|Activate|Rewrap|Invalidate|Delete|Reconcile scopeDigest:sha keyRef:ulidOrEmpty dekVersion:u32 phase:Started|Reserved|PayloadReturned|Persisted|Completed|Ambiguous|Failed attempt:u32 createdAtUtc:timestamp updatedAtUtc:timestamp result:Pending|Succeeded|Failed reasonCode:reason",
        [typeof(ReadLeaseV1)] = "operationId:ulid scopeDigest:sha keyRef:ulidOrEmpty dekVersion:u32 instanceId:ulid lifecycleEpoch:u64 issuedAtUtc:timestamp expiresAtUtc:timestamp",
        [typeof(CompletionLeaseV1)] = "operationId:ulid scopeDigest:sha keyRef:ulidOrEmpty dekVersion:u32 instanceId:ulid lifecycleEpoch:u64 issuedAtUtc:timestamp expiresAtUtc:timestamp",
        [typeof(ReconciliationV1)] = "operationId:ulid scopeDigest:sha keyRef:ulidOrEmpty dekVersion:u32 kind:Create|Activate|Rewrap|Invalidate|Delete notBeforeUtc:timestamp attempt:u32 status:Pending|Running|Quarantined|Completed reasonCode:reason updatedAtUtc:timestamp",
        [typeof(MemberHeadV1)] = "tenantDomainDigest:sha revision:u64 pageCount:u32 entryCount:u32 updatedAtUtc:timestamp",
        [typeof(MemberPageV1)] = "tenantDomainDigest:sha page:u32 entries:[MemberEntryV1]",
        [typeof(MemberEntryV1)] = "ordinal:u32 generation:u64 instanceId:ulid state:Serving|Draining|Cleared",
        [typeof(WorkHeadV1)] = "tenantDomainDigest:sha revision:u64 pageCount:u32 entryCount:u32 updatedAtUtc:timestamp",
        [typeof(WorkPageV1)] = "tenantDomainDigest:sha page:u32 entries:[WorkEntryV1]",
        [typeof(WorkEntryV1)] = "operationId:ulid scopeDigest:sha",
        [typeof(InvalidationDrainV1)] = "tenantDomainDigest:sha scopeDigest:sha operationId:ulid lifecycleEpoch:u64 memberCutoff:u32",
        [typeof(CacheClearAckV1)] = "scopeDigest:sha operationId:ulid lifecycleEpoch:u64 instanceId:ulid acknowledgedAtUtc:timestamp",
        [typeof(PayloadProtectionAuditRecord)] = "auditId:ulid operationId:ulid workflowId:ulid? correlationId:ulid? tenantId:tenant domain:domain aggregateId:aggregate? operation:Create|Activate|Rewrap|Invalidate|Delete|Reconcile fromState:Reserved|Active|RewrapPending|Invalidating|Invalidated|Deleted|Pending? toState:Reserved|Active|RewrapPending|Invalidating|Invalidated|Deleted|Pending? lifecycleEpoch:u64 fenceApprovalEpoch:u64 backendId:id formatVersion:v2 keyFingerprint:fingerprint? providerFingerprint:fingerprint? attemptCount:u32 durationMilliseconds:u64 result:Pending|Succeeded|Failed reasonCode:reason decisionActorId:actor decidedAtUtc:timestamp",
    }.ToFrozenDictionary();

    /// <summary>Gets the nested element schemas by exact name.</summary>
    internal static readonly FrozenDictionary<string, Type> Elements = new Dictionary<string, Type>
    {
        ["MemberEntryV1"] = typeof(MemberEntryV1),
        ["ScopeIndexEntryV1"] = typeof(ScopeIndexEntryV1),
        ["KekInventoryEntryV1"] = typeof(KekInventoryEntryV1),
        ["WorkEntryV1"] = typeof(WorkEntryV1),
    }.ToFrozenDictionary(StringComparer.Ordinal);
}
