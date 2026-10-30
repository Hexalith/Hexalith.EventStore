using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Closed content-free whole-snapshot erasure descriptor. No plaintext siblings, field names, values or shape from an opaque body are retained.</summary>
internal sealed record WholeSnapshotReplayMetadata(string Format, string Layout, ProtectionTarget Target)
{
    internal const string CurrentFormat = "hexalith-whole-snapshot-replay-v1";
    internal static byte[] Create(byte[] clear, ProtectionTarget target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var document = JsonDocument.Parse(clear);
        var root = document.RootElement;
        // This is the only retained technical schema: the existing literal ProtectedContent
        // envelope with its complete independent target. All encrypted siblings are omitted.
        bool envelope = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("ProtectedContent", out var content)
            && content.ValueKind == JsonValueKind.Object && content.EnumerateObject().Count() == 2
            && content.TryGetProperty("Target", out var declared) && content.TryGetProperty("Value", out _)
            && declared.Deserialize(InteractionSnapshotJsonContext.Default.ProtectionTarget) == target;
        return JsonSerializer.SerializeToUtf8Bytes(new WholeSnapshotReplayMetadata(CurrentFormat, envelope ? "protected-content" : "opaque", target),
            InteractionSnapshotJsonContext.Default.WholeSnapshotReplayMetadata);
    }
    internal static byte[] Materialize(byte[] metadata, ProtectionTarget target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // Legacy whole-object skeletons may already contain copied confidential plaintext.
        // They cannot be retrospectively certified erased; independent sanitization is required.
        var descriptor = JsonSerializer.Deserialize(metadata, InteractionSnapshotJsonContext.Default.WholeSnapshotReplayMetadata)
            ?? throw new PayloadProtectionFormatException();
        if (descriptor.Format != CurrentFormat || descriptor.Target != target || descriptor.Layout is not ("protected-content" or "opaque"))
        { throw new PayloadProtectionFormatException(); }
        JsonNode erased = JsonSerializer.SerializeToNode(new Erased(), InteractionSnapshotJsonContext.Default.Erased)!;
        JsonNode replay = descriptor.Layout == "opaque" ? erased : new JsonObject
        { ["ProtectedContent"] = new JsonObject { ["Target"] = JsonSerializer.SerializeToNode(target, InteractionSnapshotJsonContext.Default.ProtectionTarget), ["Value"] = erased } };
        return JsonSerializer.SerializeToUtf8Bytes(replay, InteractionSnapshotJsonContext.Default.JsonNode);
    }
}
