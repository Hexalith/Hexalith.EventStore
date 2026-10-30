using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Reflection-free JSON metadata for the existing snapshot wire records and exact entry identity bytes.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ProtectionTarget))]
[JsonSerializable(typeof(ProtectedSnapshotPayloadV2))]
[JsonSerializable(typeof(ProtectedSnapshotEntriesV2))]
[JsonSerializable(typeof(Erased))]
[JsonSerializable(typeof(WholeSnapshotReplayMetadata))]
[JsonSerializable(typeof(SnapshotEntryFragment))]
[JsonSerializable(typeof(SnapshotEntryOccurrenceIdentity))]
[JsonSerializable(typeof(JsonNode))]
internal partial class InteractionSnapshotJsonContext : JsonSerializerContext;
