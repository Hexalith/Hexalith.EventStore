using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Owns a bounded UTF-8 JSON snapshot and a payload-proportional byte-range index.
/// </summary>
internal sealed class BoundedJsonDocument : IDisposable
{
    private static readonly byte[] _protectedMemberName = "$pdenc"u8.ToArray();
    private readonly byte[] _utf8Json;
    private readonly List<BoundedJsonNode> _nodes;
    private readonly Dictionary<BoundedJsonLookupKey, int> _children;
    private readonly Dictionary<BoundedJsonLookupKey, List<int>> _hashCollisions;
    private readonly ISensitiveBufferObserver? _observer;
    private readonly SensitiveBufferKind _ownedBufferKind;
    private readonly bool _ownsBuffer;
    private bool _disposed;

    private BoundedJsonDocument(
        byte[] utf8Json,
        List<BoundedJsonNode> nodes,
        Dictionary<BoundedJsonLookupKey, int> children,
        Dictionary<BoundedJsonLookupKey, List<int>> hashCollisions,
        bool containsProtectedMember,
        int maximumDepth,
        bool ownsBuffer,
        ISensitiveBufferObserver? observer,
        SensitiveBufferKind ownedBufferKind)
    {
        _utf8Json = utf8Json;
        _nodes = nodes;
        _children = children;
        _hashCollisions = hashCollisions;
        ContainsProtectedMember = containsProtectedMember;
        MaximumDepth = maximumDepth;
        _ownsBuffer = ownsBuffer;
        _observer = observer;
        _ownedBufferKind = ownedBufferKind;
    }

    /// <summary>Gets the number of scalar and container nodes, including the root.</summary>
    internal int NodeCount => _nodes.Count;

    /// <summary>Gets the maximum zero-based node depth in the document.</summary>
    internal int MaximumDepth { get; }

    /// <summary>Gets a value indicating whether any object contains the reserved <c>$pdenc</c> member.</summary>
    internal bool ContainsProtectedMember { get; }

    /// <summary>Gets the root JSON node.</summary>
    internal BoundedJsonNode Root => _nodes[0];

    /// <summary>
    /// Gets the zero-based structural depth of an indexed node.
    /// </summary>
    internal static int GetDepth(BoundedJsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Depth;
    }

    /// <summary>
    /// Gets the complete indexed subtree size for one node.
    /// </summary>
    internal static int GetSubtreeNodeCount(BoundedJsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.SubtreeNodeCount;
    }

    /// <summary>
    /// Copies caller bytes once, then parses and validates only that stable snapshot.
    /// </summary>
    internal static BoundedJsonDocument Parse(
        ReadOnlySpan<byte> utf8Json,
        CancellationToken cancellationToken,
        Action<int>? checkpoint = null,
        ISensitiveBufferObserver? observer = null,
        SensitiveBufferKind ownedBufferKind = SensitiveBufferKind.InputSnapshot)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (utf8Json.Length > PayloadProtectionLimits.PayloadBytes)
        {
            throw new PayloadProtectionFormatException();
        }

        byte[] ownedJson = utf8Json.ToArray();
        return ParseCore(ownedJson, ownsBuffer: true, cancellationToken, checkpoint, observer, ownedBufferKind);
    }

    /// <summary>
    /// Parses a stable engine-owned buffer without making another plaintext copy.
    /// </summary>
    internal static BoundedJsonDocument Inspect(
        byte[] stableUtf8Json,
        CancellationToken cancellationToken,
        Action<int>? checkpoint = null,
        int maximumNodes = PayloadProtectionLimits.JsonNodes,
        int maximumDepth = PayloadProtectionLimits.JsonDepth)
    {
        ArgumentNullException.ThrowIfNull(stableUtf8Json);
        return ParseCore(
            stableUtf8Json,
            ownsBuffer: false,
            cancellationToken,
            checkpoint,
            observer: null,
            SensitiveBufferKind.InputSnapshot,
            maximumNodes,
            maximumDepth);
    }

    /// <summary>
    /// Resolves one validated canonical JSON pointer without decoding or retaining document member-name strings.
    /// </summary>
    internal BoundedJsonNode Resolve(
        string pointer,
        bool allowRoot = false,
        CancellationToken cancellationToken = default,
        Action<int>? checkpoint = null)
    {
        IReadOnlyList<string> segments = JsonPointer.Decode(
            pointer,
            allowRoot,
            cancellationToken,
            checkpoint);
        int currentIndex = 0;
        int comparisons = 0;
        Span<byte> digest = stackalloc byte[32];
        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            CheckCancellation(ref comparisons, cancellationToken, checkpoint);
            BoundedJsonNode current = _nodes[currentIndex];
            if (current.ValueKind == JsonValueKind.Object)
            {
                byte[] segment = CanonicalText.Encode(segments[segmentIndex], 0, PayloadProtectionLimits.PathBytes);
                try
                {
                    SHA256.HashData(segment, digest);
                    var key = new BoundedJsonLookupKey(
                        currentIndex,
                        IsArrayIndex: false,
                        System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(digest));
                    if (!_children.TryGetValue(key, out int childIndex))
                    {
                        throw new PayloadProtectionFormatException();
                    }

                    CheckCancellation(ref comparisons, cancellationToken, checkpoint);
                    if (!PropertyNameEquals(_nodes[childIndex], segment))
                    {
                        if (!_hashCollisions.TryGetValue(key, out List<int>? collisions))
                        {
                            throw new PayloadProtectionFormatException();
                        }

                        childIndex = -1;
                        for (int index = 0; index < collisions.Count; index++)
                        {
                            CheckCancellation(ref comparisons, cancellationToken, checkpoint);
                            if (PropertyNameEquals(_nodes[collisions[index]], segment))
                            {
                                childIndex = collisions[index];
                                break;
                            }
                        }

                        if (childIndex < 0)
                        {
                            throw new PayloadProtectionFormatException();
                        }
                    }

                    currentIndex = childIndex;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(segment);
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                int target = JsonPointer.ParseArrayIndex(segments[segmentIndex]);
                var key = new BoundedJsonLookupKey(currentIndex, IsArrayIndex: true, checked((ulong)target));
                if (!_children.TryGetValue(key, out int childIndex))
                {
                    throw new PayloadProtectionFormatException();
                }

                currentIndex = childIndex;
            }
            else
            {
                throw new PayloadProtectionFormatException();
            }
        }

        return _nodes[currentIndex];
    }

    /// <summary>
    /// Copies the exact original token bytes for one indexed value.
    /// </summary>
    internal byte[] CopyRawValue(BoundedJsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _utf8Json.AsSpan(node.Start, node.Length).ToArray();
    }

    /// <summary>
    /// Copies the complete validated payload for ownership transfer.
    /// </summary>
    internal byte[] CopyPayload(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] result = [.. _utf8Json];
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch
        {
            Clear(result, SensitiveBufferKind.AbandonedOutput, _observer);
            throw;
        }
    }

    /// <summary>
    /// Enumerates exact wrapper objects and materializes only their bounded canonical paths.
    /// </summary>
    internal IReadOnlyList<ProtectedWrapper> ReadProtectedWrappers(CancellationToken cancellationToken)
        => ReadProtectedWrappers(cancellationToken, checkpoint: null);

    /// <summary>
    /// Enumerates exact wrapper objects with a bounded cancellation checkpoint seam.
    /// </summary>
    internal IReadOnlyList<ProtectedWrapper> ReadProtectedWrappers(
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        var wrappers = new List<ProtectedWrapper>();
        int examinedChildren = 0;
        try
        {
            for (int nodeIndex = 0; nodeIndex < _nodes.Count; nodeIndex++)
            {
                if (nodeIndex == 0 || (nodeIndex & 255) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                BoundedJsonNode node = _nodes[nodeIndex];
                if (node.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                int childIndex = node.FirstChildIndex;
                int childCount = 0;
                int protectedChildIndex = -1;
                while (childIndex >= 0)
                {
                    CheckCancellation(ref examinedChildren, cancellationToken, checkpoint);
                    childCount++;
                    BoundedJsonNode child = _nodes[childIndex];
                    if (PropertyNameEquals(child, _protectedMemberName))
                    {
                        if (child.PropertyNameIsEscaped)
                        {
                            throw new PayloadProtectionFormatException();
                        }

                        protectedChildIndex = childIndex;
                    }

                    childIndex = _nodes[childIndex].NextSiblingIndex;
                }

                if (protectedChildIndex < 0)
                {
                    continue;
                }

                BoundedJsonNode protectedChild = _nodes[protectedChildIndex];
                if (node.ParentIndex < 0
                    || childCount != 1
                    || protectedChild.ValueKind != JsonValueKind.String
                    || protectedChild.ValueIsEscaped
                    || protectedChild.Length < 2)
                {
                    throw new PayloadProtectionFormatException();
                }

                if (wrappers.Count >= PayloadProtectionLimits.ProtectedPaths)
                {
                    throw new PayloadProtectionFormatException();
                }

                _ = wrappers.EnsureCapacity(checked(wrappers.Count + 1));
                ReadOnlySpan<byte> encodedEnvelope = _utf8Json.AsSpan(protectedChild.Start + 1, protectedChild.Length - 2);
                byte[] envelopeBytes = Base64UrlCodec.Decode(encodedEnvelope);
                PayloadProtectionEnvelope? envelope = null;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    envelope = EnvelopeCodec.Read(envelopeBytes);
                    cancellationToken.ThrowIfCancellationRequested();
                    string path = MaterializePath(nodeIndex, cancellationToken, checkpoint);
                    var wrapper = new ProtectedWrapper(path, envelope, node.Start, node.Length);
                    wrappers.Add(wrapper);
                    envelope = null;
                }
                finally
                {
                    Clear(envelopeBytes, SensitiveBufferKind.ProtectedOutput, _observer);
                    if (envelope is not null)
                    {
                        Clear(envelope.Nonce, SensitiveBufferKind.ProtectedOutput, _observer);
                        Clear(envelope.Ciphertext, SensitiveBufferKind.ProtectedOutput, _observer);
                        Clear(envelope.Tag, SensitiveBufferKind.ProtectedOutput, _observer);
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return wrappers;
        }
        catch
        {
            for (int index = 0; index < wrappers.Count; index++)
            {
                PayloadProtectionEnvelope envelope = wrappers[index].Envelope;
                Clear(envelope.Nonce, SensitiveBufferKind.ProtectedOutput, _observer);
                Clear(envelope.Ciphertext, SensitiveBufferKind.ProtectedOutput, _observer);
                Clear(envelope.Tag, SensitiveBufferKind.ProtectedOutput, _observer);
            }

            throw;
        }
    }

    /// <summary>
    /// Applies non-overlapping byte-range replacements and rejects output expansion beyond the payload ceiling.
    /// </summary>
    internal byte[] Rewrite(
        IReadOnlyList<JsonReplacement> replacements,
        CancellationToken cancellationToken,
        Action<int>? checkpoint = null,
        Action<int>? sortCheckpoint = null)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        var ordered = new JsonReplacement[replacements.Count];
        int examined = 0;
        for (int index = 0; index < replacements.Count; index++)
        {
            CheckCancellation(ref examined, cancellationToken, checkpoint);
            ordered[index] = replacements[index];
        }

        cancellationToken.ThrowIfCancellationRequested();
        int comparisons = 0;
        try
        {
            Array.Sort(ordered, (left, right) =>
            {
                CheckCancellation(ref comparisons, cancellationToken, sortCheckpoint);
                return left.Start.CompareTo(right.Start);
            });
        }
        catch (InvalidOperationException exception)
            when (cancellationToken.IsCancellationRequested
                && exception.InnerException is OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }

        cancellationToken.ThrowIfCancellationRequested();
        long outputLength = _utf8Json.Length;
        int previousEnd = 0;
        for (int index = 0; index < ordered.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonReplacement replacement = ordered[index];
            if (replacement.Start < previousEnd
                || replacement.Start < 0
                || replacement.Length < 0
                || replacement.Start > _utf8Json.Length - replacement.Length)
            {
                throw new PayloadProtectionFormatException();
            }

            previousEnd = checked(replacement.Start + replacement.Length);
            outputLength = checked(outputLength - replacement.Length + replacement.Value.Length);
            if (outputLength > PayloadProtectionLimits.PayloadBytes)
            {
                throw new PayloadProtectionFormatException();
            }
        }

        byte[] output = new byte[checked((int)outputLength)];
        try
        {
            int sourceOffset = 0;
            int destinationOffset = 0;
            int copiedBytes = 0;
            for (int index = 0; index < ordered.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                JsonReplacement replacement = ordered[index];
                int unchangedLength = replacement.Start - sourceOffset;
                CopyWithCancellation(
                    _utf8Json.AsSpan(sourceOffset, unchangedLength),
                    output.AsSpan(destinationOffset),
                    ref copiedBytes,
                    cancellationToken,
                    checkpoint);
                destinationOffset += unchangedLength;
                CopyWithCancellation(
                    replacement.Value,
                    output.AsSpan(destinationOffset),
                    ref copiedBytes,
                    cancellationToken,
                    checkpoint);
                destinationOffset += replacement.Value.Length;
                sourceOffset = checked(replacement.Start + replacement.Length);
            }

            CopyWithCancellation(
                _utf8Json.AsSpan(sourceOffset),
                output.AsSpan(destinationOffset),
                ref copiedBytes,
                cancellationToken,
                checkpoint);
            cancellationToken.ThrowIfCancellationRequested();
            return output;
        }
        catch
        {
            Clear(output, SensitiveBufferKind.AbandonedOutput, _observer);
            throw;
        }
    }

    private static void CopyWithCancellation(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        ref int copiedBytes,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        int offset = 0;
        while (offset < source.Length)
        {
            if (copiedBytes == 0)
            {
                checkpoint?.Invoke(1);
                cancellationToken.ThrowIfCancellationRequested();
            }

            int nextBoundary = checked(((copiedBytes / 256) + 1) * 256);
            int length = Math.Min(nextBoundary - copiedBytes, source.Length - offset);
            source.Slice(offset, length).CopyTo(destination.Slice(offset, length));
            offset += length;
            copiedBytes = checked(copiedBytes + length);
            if (copiedBytes == nextBoundary)
            {
                checkpoint?.Invoke(copiedBytes);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsBuffer)
        {
            Clear(_utf8Json, _ownedBufferKind, _observer);
        }
    }

    private static BoundedJsonDocument ParseCore(
        byte[] utf8Json,
        bool ownsBuffer,
        CancellationToken cancellationToken,
        Action<int>? checkpoint,
        ISensitiveBufferObserver? observer,
        SensitiveBufferKind ownedBufferKind,
        int maximumNodes = PayloadProtectionLimits.JsonNodes,
        int maximumDepth = PayloadProtectionLimits.JsonDepth)
    {
        Stack<JsonContainerFrame>? containers = null;
        bool containsProtectedMember = false;
        int observedMaximumDepth = 0;
        bool success = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (utf8Json.Length > PayloadProtectionLimits.PayloadBytes
                || maximumNodes is < 1 or > PayloadProtectionLimits.JsonNodes
                || maximumDepth is < 1 or > PayloadProtectionLimits.JsonDepth)
            {
                throw new PayloadProtectionFormatException();
            }

            CanonicalText.ValidateUtf8(utf8Json);

            List<BoundedJsonNode>? nodes = [];
            containers = [];
            var reader = new Utf8JsonReader(
                utf8Json,
                new JsonReaderOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = maximumDepth,
                });

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        if (containers.Count == 0 || containers.Peek().ValueKind != JsonValueKind.Object)
                        {
                            throw new PayloadProtectionFormatException();
                        }

                        containsProtectedMember |= reader.ValueTextEquals(_protectedMemberName);
                        containers.Peek().SetProperty(
                            reader,
                            checked((int)reader.TokenStartIndex),
                            checked((int)(reader.BytesConsumed - reader.TokenStartIndex)));
                        break;
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                    case JsonTokenType.String:
                    case JsonTokenType.Number:
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                    case JsonTokenType.Null:
                        int nodeIndex = AddNode(
                            reader,
                            nodes,
                            containers,
                            cancellationToken,
                            checkpoint,
                            maximumNodes);
                        observedMaximumDepth = Math.Max(observedMaximumDepth, nodes[nodeIndex].Depth);
                        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                        {
                            containers.Push(new JsonContainerFrame(
                                nodeIndex,
                                reader.TokenType == JsonTokenType.StartObject
                                    ? JsonValueKind.Object
                                    : JsonValueKind.Array));
                        }

                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        if (containers.Count == 0)
                        {
                            throw new PayloadProtectionFormatException();
                        }

                        JsonContainerFrame frame = containers.Pop();
                        try
                        {
                            JsonValueKind expectedKind = reader.TokenType == JsonTokenType.EndObject
                                ? JsonValueKind.Object
                                : JsonValueKind.Array;
                            if (frame.ValueKind != expectedKind)
                            {
                                throw new PayloadProtectionFormatException();
                            }

                            frame.ValidateComplete();
                            nodes[frame.NodeIndex].Length = checked((int)reader.BytesConsumed - nodes[frame.NodeIndex].Start);
                            nodes[frame.NodeIndex].SubtreeNodeCount = checked(nodes.Count - frame.NodeIndex);
                        }
                        finally
                        {
                            frame.Dispose();
                        }

                        break;
                }
            }

            if (nodes.Count == 0 || nodes[0].ParentIndex >= 0 || containers.Count != 0)
            {
                throw new PayloadProtectionFormatException();
            }

            cancellationToken.ThrowIfCancellationRequested();
            CreateLookups(
                nodes,
                cancellationToken,
                checkpoint,
                out Dictionary<BoundedJsonLookupKey, int> children,
                out Dictionary<BoundedJsonLookupKey, List<int>> hashCollisions);
            cancellationToken.ThrowIfCancellationRequested();
            var result = new BoundedJsonDocument(
                utf8Json,
                nodes,
                children,
                hashCollisions,
                containsProtectedMember,
                observedMaximumDepth,
                ownsBuffer,
                observer,
                ownedBufferKind);
            success = true;
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PayloadProtectionFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or OverflowException)
        {
            throw new PayloadProtectionFormatException();
        }
        finally
        {
            while (containers is not null && containers.Count > 0)
            {
                containers.Pop().Dispose();
            }

            if (!success && ownsBuffer)
            {
                Clear(utf8Json, ownedBufferKind, observer);
            }
        }
    }

    private static int AddNode(
        Utf8JsonReader reader,
        List<BoundedJsonNode> nodes,
        Stack<JsonContainerFrame> containers,
        CancellationToken cancellationToken,
        Action<int>? checkpoint,
        int maximumNodes)
    {
        int count = checked(nodes.Count + 1);
        bool container = reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray;
        if (container || count == 1 || (count & 255) == 0)
        {
            checkpoint?.Invoke(count);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (count > maximumNodes)
        {
            throw new PayloadProtectionFormatException();
        }

        int parentIndex = containers.Count == 0 ? -1 : containers.Peek().NodeIndex;
        int propertyStart = -1;
        int propertyLength = 0;
        ulong propertyNameHash = 0;
        bool propertyNameIsEscaped = false;
        int arrayIndex = -1;
        if (containers.Count > 0)
        {
            JsonContainerFrame parent = containers.Peek();
            if (parent.ValueKind == JsonValueKind.Object)
            {
                parent.ConsumeProperty(
                    out propertyStart,
                    out propertyLength,
                    out propertyNameHash,
                    out propertyNameIsEscaped);
            }
            else
            {
                arrayIndex = parent.NextArrayIndex++;
            }
        }

        JsonValueKind valueKind = reader.TokenType switch
        {
            JsonTokenType.StartObject => JsonValueKind.Object,
            JsonTokenType.StartArray => JsonValueKind.Array,
            JsonTokenType.String => JsonValueKind.String,
            JsonTokenType.Number => JsonValueKind.Number,
            JsonTokenType.True => JsonValueKind.True,
            JsonTokenType.False => JsonValueKind.False,
            JsonTokenType.Null => JsonValueKind.Null,
            _ => JsonValueKind.Undefined,
        };
        int start = checked((int)reader.TokenStartIndex);
        int length = container ? 0 : checked((int)reader.BytesConsumed - start);
        var node = new BoundedJsonNode(
            valueKind,
            start,
            length,
            parentIndex,
            propertyStart,
            propertyLength,
            propertyNameHash,
            arrayIndex,
            containers.Count,
            propertyNameIsEscaped,
            reader.TokenType == JsonTokenType.String && reader.ValueIsEscaped);
        int nodeIndex = nodes.Count;
        nodes.Add(node);
        if (parentIndex >= 0)
        {
            BoundedJsonNode parentNode = nodes[parentIndex];
            if (parentNode.FirstChildIndex < 0)
            {
                parentNode.FirstChildIndex = nodeIndex;
            }
            else
            {
                nodes[parentNode.LastChildIndex].NextSiblingIndex = nodeIndex;
            }

            parentNode.LastChildIndex = nodeIndex;
        }

        return nodeIndex;
    }

    private static void CreateLookups(
        List<BoundedJsonNode> nodes,
        CancellationToken cancellationToken,
        Action<int>? checkpoint,
        out Dictionary<BoundedJsonLookupKey, int> children,
        out Dictionary<BoundedJsonLookupKey, List<int>> hashCollisions)
    {
        children = new Dictionary<BoundedJsonLookupKey, int>(nodes.Count);
        hashCollisions = [];
        int examined = 0;
        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            CheckCancellation(ref examined, cancellationToken, checkpoint);
            BoundedJsonNode node = nodes[nodeIndex];
            if (node.ParentIndex < 0)
            {
                continue;
            }

            var key = node.PropertyTokenStart >= 0
                ? new BoundedJsonLookupKey(node.ParentIndex, IsArrayIndex: false, node.PropertyNameHash)
                : new BoundedJsonLookupKey(node.ParentIndex, IsArrayIndex: true, checked((ulong)node.ArrayIndex));
            if (children.TryAdd(key, nodeIndex))
            {
                continue;
            }

            if (key.IsArrayIndex)
            {
                throw new PayloadProtectionFormatException();
            }

            if (!hashCollisions.TryGetValue(key, out List<int>? collisions))
            {
                collisions = [];
                hashCollisions.Add(key, collisions);
            }

            collisions.Add(nodeIndex);
        }
    }

    private string MaterializePath(
        int nodeIndex,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        Span<int> ancestors = stackalloc int[PayloadProtectionLimits.JsonDepth + 1];
        int count = 0;
        int current = nodeIndex;
        int examined = 0;
        while (_nodes[current].ParentIndex >= 0)
        {
            CheckCancellation(ref examined, cancellationToken, checkpoint);
            if (count == ancestors.Length)
            {
                throw new PayloadProtectionFormatException();
            }

            ancestors[count++] = current;
            current = _nodes[current].ParentIndex;
        }

        byte[] path = new byte[PayloadProtectionLimits.PathBytes + 1];
        int written = 0;
        try
        {
            Span<byte> formattedArrayIndex = stackalloc byte[10];
            for (int index = count - 1; index >= 0; index--)
            {
                CheckCancellation(ref examined, cancellationToken, checkpoint);
                BoundedJsonNode node = _nodes[ancestors[index]];
                WritePathByte(path, ref written, (byte)'/', ref examined, cancellationToken, checkpoint);
                if (node.PropertyTokenStart >= 0)
                {
                    WriteEscapedPropertyName(
                        node,
                        path,
                        ref written,
                        ref examined,
                        cancellationToken,
                        checkpoint);
                }
                else if (node.ArrayIndex >= 0)
                {
                    if (!Utf8Formatter.TryFormat(node.ArrayIndex, formattedArrayIndex, out int formattedLength))
                    {
                        throw new PayloadProtectionFormatException();
                    }

                    WritePathBytes(
                        path,
                        ref written,
                        formattedArrayIndex[..formattedLength],
                        ref examined,
                        cancellationToken,
                        checkpoint);
                }
                else
                {
                    throw new PayloadProtectionFormatException();
                }
            }

            if (written == 0 || written > PayloadProtectionLimits.PathBytes)
            {
                throw new PayloadProtectionFormatException();
            }

            return Encoding.UTF8.GetString(path, 0, written);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(path);
        }
    }

    private void WriteEscapedPropertyName(
        BoundedJsonNode node,
        byte[] path,
        ref int written,
        ref int examined,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        ReadOnlySpan<byte> token = _utf8Json.AsSpan(node.PropertyTokenStart, node.PropertyTokenLength);
        if (token.Length < 2 || token[0] != (byte)'\"')
        {
            throw new PayloadProtectionFormatException();
        }

        int contentEnd = FindPropertyNameEnd(
            token,
            ref examined,
            cancellationToken,
            checkpoint);
        Span<byte> encoded = stackalloc byte[4];
        for (int index = 1; index < contentEnd; index++)
        {
            CheckCancellation(ref examined, cancellationToken, checkpoint);
            byte value = token[index];
            if (value != (byte)'\\')
            {
                WriteEscapedPathValue(path, ref written, value, ref examined, cancellationToken, checkpoint);
                continue;
            }

            if (++index >= contentEnd)
            {
                throw new PayloadProtectionFormatException();
            }

            CheckCancellation(ref examined, cancellationToken, checkpoint);
            byte escape = token[index];
            if (escape != (byte)'u')
            {
                byte decoded = escape switch
                {
                    (byte)'\"' => (byte)'\"',
                    (byte)'\\' => (byte)'\\',
                    (byte)'/' => (byte)'/',
                    (byte)'b' => (byte)'\b',
                    (byte)'f' => (byte)'\f',
                    (byte)'n' => (byte)'\n',
                    (byte)'r' => (byte)'\r',
                    (byte)'t' => (byte)'\t',
                    _ => throw new PayloadProtectionFormatException(),
                };
                WriteEscapedPathValue(path, ref written, decoded, ref examined, cancellationToken, checkpoint);
                continue;
            }

            int scalar = ReadHexCodeUnit(
                token,
                contentEnd,
                ref index,
                ref examined,
                cancellationToken,
                checkpoint);
            if (scalar is >= 0xd800 and <= 0xdbff)
            {
                if (index + 2 >= contentEnd
                    || token[index + 1] != (byte)'\\'
                    || token[index + 2] != (byte)'u')
                {
                    throw new PayloadProtectionFormatException();
                }

                index += 2;
                int lowSurrogate = ReadHexCodeUnit(
                    token,
                    contentEnd,
                    ref index,
                    ref examined,
                    cancellationToken,
                    checkpoint);
                if (lowSurrogate is < 0xdc00 or > 0xdfff)
                {
                    throw new PayloadProtectionFormatException();
                }

                scalar = 0x10000 + ((scalar - 0xd800) << 10) + (lowSurrogate - 0xdc00);
            }
            else if (scalar is >= 0xdc00 and <= 0xdfff)
            {
                throw new PayloadProtectionFormatException();
            }

            if (!Rune.TryCreate(scalar, out Rune rune))
            {
                throw new PayloadProtectionFormatException();
            }

            int encodedLength = rune.EncodeToUtf8(encoded);
            for (int encodedIndex = 0; encodedIndex < encodedLength; encodedIndex++)
            {
                WriteEscapedPathValue(
                    path,
                    ref written,
                    encoded[encodedIndex],
                    ref examined,
                    cancellationToken,
                    checkpoint);
            }
        }
    }

    private static int FindPropertyNameEnd(
        ReadOnlySpan<byte> token,
        ref int examined,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        bool escaped = false;
        for (int index = 1; index < token.Length; index++)
        {
            CheckCancellation(ref examined, cancellationToken, checkpoint);
            byte value = token[index];
            if (escaped)
            {
                escaped = false;
            }
            else if (value == (byte)'\\')
            {
                escaped = true;
            }
            else if (value == (byte)'\"')
            {
                return index;
            }
        }

        throw new PayloadProtectionFormatException();
    }

    private static int ReadHexCodeUnit(
        ReadOnlySpan<byte> token,
        int contentEnd,
        ref int index,
        ref int examined,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        if (index > contentEnd - 5)
        {
            throw new PayloadProtectionFormatException();
        }

        int value = 0;
        for (int digit = 0; digit < 4; digit++)
        {
            index++;
            CheckCancellation(ref examined, cancellationToken, checkpoint);
            byte character = token[index];
            int nibble = character switch
            {
                >= (byte)'0' and <= (byte)'9' => character - (byte)'0',
                >= (byte)'A' and <= (byte)'F' => character - (byte)'A' + 10,
                >= (byte)'a' and <= (byte)'f' => character - (byte)'a' + 10,
                _ => throw new PayloadProtectionFormatException(),
            };
            value = (value << 4) | nibble;
        }

        return value;
    }

    private static void WriteEscapedPathValue(
        byte[] path,
        ref int written,
        byte value,
        ref int examined,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        if (value == (byte)'~')
        {
            WritePathBytes(path, ref written, "~0"u8, ref examined, cancellationToken, checkpoint);
        }
        else if (value == (byte)'/')
        {
            WritePathBytes(path, ref written, "~1"u8, ref examined, cancellationToken, checkpoint);
        }
        else
        {
            WritePathByte(path, ref written, value, ref examined, cancellationToken, checkpoint);
        }
    }

    private bool PropertyNameEquals(BoundedJsonNode node, ReadOnlySpan<byte> expected)
    {
        if (node.PropertyTokenStart < 0)
        {
            return false;
        }

        var reader = new Utf8JsonReader(_utf8Json.AsSpan(node.PropertyTokenStart, node.PropertyTokenLength));
        return reader.Read()
            && reader.TokenType == JsonTokenType.String
            && reader.ValueTextEquals(expected);
    }

    private static void CheckCancellation(
        ref int examined,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        examined = checked(examined + 1);
        if (examined == 1 || (examined & 255) == 0)
        {
            checkpoint?.Invoke(examined);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static void WritePathBytes(
        byte[] destination,
        ref int written,
        ReadOnlySpan<byte> value,
        ref int examined,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        if (value.Length > PayloadProtectionLimits.PathBytes - written)
        {
            throw new PayloadProtectionFormatException();
        }

        for (int index = 0; index < value.Length; index++)
        {
            WritePathByte(destination, ref written, value[index], ref examined, cancellationToken, checkpoint);
        }
    }

    private static void WritePathByte(
        byte[] destination,
        ref int written,
        byte value,
        ref int examined,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        CheckCancellation(ref examined, cancellationToken, checkpoint);
        if (written >= PayloadProtectionLimits.PathBytes)
        {
            throw new PayloadProtectionFormatException();
        }

        destination[written++] = value;
    }

    private static void Clear(byte[] buffer, SensitiveBufferKind kind, ISensitiveBufferObserver? observer)
    {
        CryptographicOperations.ZeroMemory(buffer);
        if (observer is null)
        {
            return;
        }

        try
        {
            observer.BufferCleared(kind, buffer);
        }
        catch
        {
            // Diagnostic test observers are best effort and cannot weaken cleanup or operation semantics.
        }
    }
}
