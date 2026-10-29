using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Bounded non-overlapping protected snapshot values and their content-free technical skeleton.</summary>
internal sealed class SnapshotEntryLayout
{
    internal required byte[] Skeleton { get; init; }
    internal required SnapshotProtectionEntry[] Entries { get; init; }
    internal required JsonNode?[] Values { get; init; }
    internal string ContainerDigest => Convert.ToHexString(SHA256.HashData(Skeleton));
    internal static SnapshotEntryLayout Read(byte[] source, string tenant, bool skeleton, CancellationToken token)
    {
        using var bounded = BoundedJsonDocument.Inspect(source, token);
        if (source.Length > PayloadProtectionLimits.CiphertextBytes || bounded.ContainsProtectedMember || bounded.ContainsLegacyProtectedMember)
        { throw new PayloadProtectionFormatException(); }
        var root = JsonNode.Parse(source) ?? throw new PayloadProtectionFormatException();
        var entries = new List<SnapshotProtectionEntry>(); var values = new List<JsonNode?>();
        void Walk(JsonNode? node, string path, bool withinValue)
        {
            token.ThrowIfCancellationRequested();
            if (node is JsonArray array) { for (int i = 0; i < array.Count; i++) { Walk(array[i], path + "/" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), withinValue); } return; }
            if (node is not JsonObject obj) { return; }
            bool addressedObject = obj.Any(pair => pair.Key.Equals("TargetProtectionKeyAlias", StringComparison.OrdinalIgnoreCase));
            if (addressedObject && (withinValue || !obj.Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal)
                .SetEquals(["TenantId", "AgentInteractionId", "TargetProtectionKeyAlias"]))) { throw new InvalidOperationException("ProtectionEnvelopeInvalid"); }
            if (addressedObject)
            {
                var declared = obj.Deserialize(InteractionSnapshotJsonContext.Default.ProtectionTarget) ?? throw new PayloadProtectionFormatException();
                if (declared.TenantId != tenant || string.IsNullOrWhiteSpace(declared.AgentInteractionId) || string.IsNullOrWhiteSpace(declared.TargetProtectionKeyAlias))
                { throw new InvalidOperationException("ProtectionEnvelopeInvalid"); }
            }
            bool envelope = obj.Any(pair => pair.Key.Equals("Target", StringComparison.OrdinalIgnoreCase) && pair.Value is JsonObject target
                && target.Any(member => member.Key.Equals("TargetProtectionKeyAlias", StringComparison.OrdinalIgnoreCase)))
                && obj.Any(pair => pair.Key.Equals("Value", StringComparison.OrdinalIgnoreCase));
            if (envelope)
            {
                if (withinValue || obj.Count != 2 || !obj.ContainsKey("Target") || !obj.ContainsKey("Value")
                    || entries.Count >= 1000 || path.Length > 2040 || !skeleton && obj["Value"] is null
                    || obj["Target"] is not JsonObject exactTarget || !exactTarget.Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal)
                        .SetEquals(["TenantId", "AgentInteractionId", "TargetProtectionKeyAlias"]))
                { throw new PayloadProtectionFormatException(); }
                var addressed = obj["Target"]!.Deserialize(InteractionSnapshotJsonContext.Default.ProtectionTarget) ?? throw new PayloadProtectionFormatException();
                if (addressed.TenantId != tenant) { throw new InvalidOperationException("ProtectionAuthorityUnavailable"); }
                if (((JsonObject)obj["Target"]!).Count != 3 || string.IsNullOrWhiteSpace(addressed.AgentInteractionId)
                    || string.IsNullOrWhiteSpace(addressed.TargetProtectionKeyAlias)) { throw new PayloadProtectionFormatException(); }
                Walk(obj["Value"], path + "/Value", true);
                entries.Add(new(path + "/Value", addressed)); values.Add(obj["Value"]?.DeepClone()); obj["Value"] = null; return;
            }
            foreach (var pair in obj.ToArray())
            {
                if (pair.Key.Equals("ProtectedContent", StringComparison.OrdinalIgnoreCase)
                    && (pair.Key != "ProtectedContent" || pair.Value is not JsonObject content || content.Count != 2
                        || content["Target"] is not JsonObject protectedTarget || !protectedTarget.ContainsKey("TargetProtectionKeyAlias") || !content.ContainsKey("Value")))
                { throw new PayloadProtectionFormatException(); }
                Walk(pair.Value, path + "/" + pair.Key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal), withinValue);
            }
        }
        Walk(root, string.Empty, false);
        return new() { Skeleton = JsonSerializer.SerializeToUtf8Bytes(root, InteractionSnapshotJsonContext.Default.JsonNode), Entries = entries.ToArray(), Values = values.ToArray() };
    }
    internal static void Replace(JsonNode root, string path, JsonNode? value)
    {
        string[] parts = path[1..].Split('/').Select(part => part.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)).ToArray();
        JsonNode current = root;
        for (int i = 0; i < parts.Length - 1; i++)
        { current = current is JsonArray array ? array[int.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture)]! : current[parts[i]]!; }
        if (current is JsonArray parent) { parent[int.Parse(parts[^1], System.Globalization.CultureInfo.InvariantCulture)] = value; }
        else { current[parts[^1]] = value; }
    }
}
