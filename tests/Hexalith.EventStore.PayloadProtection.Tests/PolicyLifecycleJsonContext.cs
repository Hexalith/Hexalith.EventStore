using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Explicit test payload metadata; no reflection JSON resolver is used.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PolicyLifecyclePayload))]
[JsonSerializable(typeof(string))]
internal partial class PolicyLifecycleJsonContext : JsonSerializerContext;
