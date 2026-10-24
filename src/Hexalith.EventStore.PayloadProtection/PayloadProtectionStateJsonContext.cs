// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(WrappedDekV1))]
[JsonSerializable(typeof(ScopeIndexHeadV1))]
[JsonSerializable(typeof(ScopeIndexPageV1))]
[JsonSerializable(typeof(ScopeIndexEntryV1))]
[JsonSerializable(typeof(KekInventoryHeadV1))]
[JsonSerializable(typeof(KekInventoryPageV1))]
[JsonSerializable(typeof(KekInventoryEntryV1))]
[JsonSerializable(typeof(LifecycleV1))]
[JsonSerializable(typeof(FenceV1))]
[JsonSerializable(typeof(DeploymentGenerationV1))]
[JsonSerializable(typeof(CapabilityLeaseV1))]
[JsonSerializable(typeof(OperationV1))]
[JsonSerializable(typeof(ReadLeaseV1))]
[JsonSerializable(typeof(CompletionLeaseV1))]
[JsonSerializable(typeof(ReconciliationV1))]
[JsonSerializable(typeof(MemberHeadV1))]
[JsonSerializable(typeof(MemberPageV1))]
[JsonSerializable(typeof(MemberEntryV1))]
[JsonSerializable(typeof(WorkHeadV1))]
[JsonSerializable(typeof(WorkPageV1))]
[JsonSerializable(typeof(WorkEntryV1))]
[JsonSerializable(typeof(InvalidationDrainV1))]
[JsonSerializable(typeof(CacheClearAckV1))]
[JsonSerializable(typeof(PayloadProtectionAuditRecord))]
/// <summary>Explicitly enrolls every durable state root and nested record for reflection-free JSON serialization.</summary>
internal partial class PayloadProtectionStateJsonContext : JsonSerializerContext;
