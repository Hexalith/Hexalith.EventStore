using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Explicit test payload metadata; no reflection JSON resolver is used.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PolicyLifecyclePayload))]
[JsonSerializable(typeof(NullablePolicyPayload))]
[JsonSerializable(typeof(NestedNullablePolicyPayload))]
[JsonSerializable(typeof(NestedSensitiveValue))]
[JsonSerializable(typeof(NestedSensitiveValue?))]
[JsonSerializable(typeof(List<NestedSensitiveValue?>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(ChangingGetterPayload))]
[JsonSerializable(typeof(RuntimeSubstitutionPayload))]
[JsonSerializable(typeof(WholeObjectSelectionPayload))]
[JsonSerializable(typeof(OversizedPolicyPayload))]
[JsonSerializable(typeof(OversizedScalarPayload))]
[JsonSerializable(typeof(PartySnapshotState))]
internal partial class PolicyLifecycleJsonContext : JsonSerializerContext;
