using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Owns a JSON document validated against the pdenc-v2 byte, depth, node, and duplicate-name limits.
/// </summary>
internal sealed class BoundedJsonDocument : IDisposable {
    private BoundedJsonDocument(JsonDocument document, JsonNode root, int nodeCount, bool containsProtectedMember) {
        Document = document;
        Root = root;
        NodeCount = nodeCount;
        ContainsProtectedMember = containsProtectedMember;
    }

    /// <summary>
    /// Gets the immutable tree whose values retain their original raw JSON token bytes.
    /// </summary>
    internal JsonDocument Document { get; }

    /// <summary>
    /// Gets the mutable transformation tree.
    /// </summary>
    internal JsonNode Root { get; }

    /// <summary>
    /// Gets the number of scalar and container nodes, including the root.
    /// </summary>
    internal int NodeCount { get; }

    /// <summary>
    /// Gets a value indicating whether any object contains the reserved <c>$pdenc</c> member.
    /// </summary>
    internal bool ContainsProtectedMember { get; }

    /// <summary>
    /// Parses and validates exactly one UTF-8 JSON value.
    /// </summary>
    internal static BoundedJsonDocument Parse(
        ReadOnlySpan<byte> utf8Json,
        CancellationToken cancellationToken,
        Action<int>? checkpoint = null) {
        cancellationToken.ThrowIfCancellationRequested();
        if (utf8Json.Length > PayloadProtectionLimits.PayloadBytes) {
            throw new PayloadProtectionFormatException();
        }

        var reader = new Utf8JsonReader(
            utf8Json,
            new JsonReaderOptions {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = PayloadProtectionLimits.JsonDepth,
            });
        var objectMembers = new Stack<HashSet<string>>();
        int nodes = 0;
        bool containsProtectedMember = false;
        try {
            while (reader.Read()) {
                switch (reader.TokenType) {
                    case JsonTokenType.StartObject:
                        CountNode(ref nodes, cancellationToken, checkpoint, container: true);
                        objectMembers.Push(new HashSet<string>(StringComparer.Ordinal));
                        break;
                    case JsonTokenType.EndObject:
                        _ = objectMembers.Pop();
                        break;
                    case JsonTokenType.StartArray:
                        CountNode(ref nodes, cancellationToken, checkpoint, container: true);
                        break;
                    case JsonTokenType.PropertyName:
                        string memberName = reader.GetString()!;
                        if (objectMembers.Count == 0 || !objectMembers.Peek().Add(memberName)) {
                            throw new PayloadProtectionFormatException();
                        }

                        containsProtectedMember |= string.Equals(memberName, "$pdenc", StringComparison.Ordinal);

                        break;
                    case JsonTokenType.String:
                    case JsonTokenType.Number:
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                    case JsonTokenType.Null:
                        CountNode(ref nodes, cancellationToken, checkpoint, container: false);
                        break;
                }
            }
        }
        catch (JsonException) {
            throw new PayloadProtectionFormatException();
        }

        try {
            byte[] ownedJson = utf8Json.ToArray();
            JsonDocument document = JsonDocument.Parse(
                ownedJson,
                new JsonDocumentOptions {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = PayloadProtectionLimits.JsonDepth,
                });
            JsonNode? node = JsonNode.Parse(
                ownedJson,
                documentOptions: new JsonDocumentOptions { MaxDepth = PayloadProtectionLimits.JsonDepth });
            if (node is null) {
                document.Dispose();
                throw new PayloadProtectionFormatException();
            }

            return new BoundedJsonDocument(document, node, nodes, containsProtectedMember);
        }
        catch (JsonException) {
            throw new PayloadProtectionFormatException();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => Document.Dispose();

    private static void CountNode(
        ref int count,
        CancellationToken cancellationToken,
        Action<int>? checkpoint,
        bool container) {
        count = checked(count + 1);
        if (container || (count & 255) == 0) {
            checkpoint?.Invoke(count);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (count > PayloadProtectionLimits.JsonNodes) {
            throw new PayloadProtectionFormatException();
        }
    }
}
