using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Exact source-generated folded snapshot carrier used by occurrence-aware protection.</summary>
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class SnapshotStateJsonContext : JsonSerializerContext;
