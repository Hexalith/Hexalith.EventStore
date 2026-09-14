using System.Buffers;
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
    private readonly ISensitiveBufferObserver? _observer;
    private readonly SensitiveBufferKind _ownedBufferKind;
    private readonly bool _ownsBuffer;
    private bool _disposed;

    private BoundedJsonDocument(
        byte[] utf8Json,
        List<BoundedJsonNode> nodes,
        bool containsProtectedMember,
        bool ownsBuffer,
        ISensitiveBufferObserver? observer,
        SensitiveBufferKind ownedBufferKind)
    {
        _utf8Json = utf8Json;
        _nodes = nodes;
        ContainsProtectedMember = containsProtectedMember;
        _ownsBuffer = ownsBuffer;
        _observer = observer;
        _ownedBufferKind = ownedBufferKind;
    }

    /// <summary>Gets the number of scalar and container nodes, including the root.</summary>
    internal int NodeCount => _nodes.Count;

    /// <summary>Gets a value indicating whether any object contains the reserved <c>$pdenc</c> member.</summary>
    internal bool ContainsProtectedMember { get; }

    /// <summary>Gets the root JSON node.</summary>
    internal BoundedJsonNode Root => _nodes[0];

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
        Action<int>? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(stableUtf8Json);
        return ParseCore(
            stableUtf8Json,
            ownsBuffer: false,
            cancellationToken,
            checkpoint,
            observer: null,
            SensitiveBufferKind.InputSnapshot);
    }

    /// <summary>
    /// Resolves one validated canonical JSON pointer without decoding or retaining document member-name strings.
    /// </summary>
    internal BoundedJsonNode Resolve(string pointer, bool allowRoot = false)
    {
        IReadOnlyList<string> segments = JsonPointer.Decode(pointer, allowRoot);
        int currentIndex = 0;
        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            BoundedJsonNode current = _nodes[currentIndex];
            if (current.ValueKind == JsonValueKind.Object)
            {
                byte[] segment = CanonicalText.Encode(segments[segmentIndex], 0, PayloadProtectionLimits.PathBytes);
                try
                {
                    int childIndex = current.FirstChildIndex;
                    while (childIndex >= 0 && !PropertyNameEquals(_nodes[childIndex], segment))
                    {
                        childIndex = _nodes[childIndex].NextSiblingIndex;
                    }

                    if (childIndex < 0)
                    {
                        throw new PayloadProtectionFormatException();
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
                int childIndex = current.FirstChildIndex;
                while (childIndex >= 0 && _nodes[childIndex].ArrayIndex != target)
                {
                    childIndex = _nodes[childIndex].NextSiblingIndex;
                }

                if (childIndex < 0)
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
        byte[] result = _utf8Json.ToArray();
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
    {
        var wrappers = new List<ProtectedWrapper>();
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
                    childCount++;
                    if (PropertyNameEquals(_nodes[childIndex], _protectedMemberName))
                    {
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

                ReadOnlySpan<byte> encodedEnvelope = _utf8Json.AsSpan(protectedChild.Start + 1, protectedChild.Length - 2);
                byte[] envelopeBytes = Base64UrlCodec.Decode(encodedEnvelope);
                try
                {
                    string path = MaterializePath(nodeIndex);
                    wrappers.Add(new ProtectedWrapper(path, EnvelopeCodec.Read(envelopeBytes), node.Start, node.Length));
                }
                finally
                {
                    Clear(envelopeBytes, SensitiveBufferKind.ProtectedOutput, _observer);
                }
            }

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
    internal byte[] Rewrite(IReadOnlyList<JsonReplacement> replacements, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        var ordered = new JsonReplacement[replacements.Count];
        for (int index = 0; index < replacements.Count; index++)
        {
            ordered[index] = replacements[index];
        }

        Array.Sort(ordered, static (left, right) => left.Start.CompareTo(right.Start));
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
            for (int index = 0; index < ordered.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                JsonReplacement replacement = ordered[index];
                int unchangedLength = replacement.Start - sourceOffset;
                _utf8Json.AsSpan(sourceOffset, unchangedLength).CopyTo(output.AsSpan(destinationOffset));
                destinationOffset += unchangedLength;
                replacement.Value.CopyTo(output, destinationOffset);
                destinationOffset += replacement.Value.Length;
                sourceOffset = checked(replacement.Start + replacement.Length);
            }

            _utf8Json.AsSpan(sourceOffset).CopyTo(output.AsSpan(destinationOffset));
            cancellationToken.ThrowIfCancellationRequested();
            return output;
        }
        catch
        {
            Clear(output, SensitiveBufferKind.AbandonedOutput, _observer);
            throw;
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
        SensitiveBufferKind ownedBufferKind)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (utf8Json.Length > PayloadProtectionLimits.PayloadBytes)
        {
            if (ownsBuffer)
            {
                Clear(utf8Json, ownedBufferKind, observer);
            }

            throw new PayloadProtectionFormatException();
        }

        var nodes = new List<BoundedJsonNode>();
        var containers = new Stack<JsonContainerFrame>();
        bool containsProtectedMember = false;
        bool success = false;
        try
        {
            var reader = new Utf8JsonReader(
                utf8Json,
                new JsonReaderOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = PayloadProtectionLimits.JsonDepth,
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
                        int nodeIndex = AddNode(reader, nodes, containers, cancellationToken, checkpoint);
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
            success = true;
            return new BoundedJsonDocument(
                utf8Json,
                nodes,
                containsProtectedMember,
                ownsBuffer,
                observer,
                ownedBufferKind);
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
            while (containers.Count > 0)
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
        Action<int>? checkpoint)
    {
        int count = checked(nodes.Count + 1);
        bool container = reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray;
        if (container || count == 1 || (count & 255) == 0)
        {
            checkpoint?.Invoke(count);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (count > PayloadProtectionLimits.JsonNodes)
        {
            throw new PayloadProtectionFormatException();
        }

        int parentIndex = containers.Count == 0 ? -1 : containers.Peek().NodeIndex;
        int propertyStart = -1;
        int propertyLength = 0;
        int arrayIndex = -1;
        if (containers.Count > 0)
        {
            JsonContainerFrame parent = containers.Peek();
            if (parent.ValueKind == JsonValueKind.Object)
            {
                parent.ConsumeProperty(out propertyStart, out propertyLength);
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
            arrayIndex,
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

    private string MaterializePath(int nodeIndex)
    {
        Span<int> ancestors = stackalloc int[PayloadProtectionLimits.JsonDepth + 1];
        int count = 0;
        int current = nodeIndex;
        while (_nodes[current].ParentIndex >= 0)
        {
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
                BoundedJsonNode node = _nodes[ancestors[index]];
                WritePathByte(path, ref written, (byte)'/');
                if (node.PropertyTokenStart >= 0)
                {
                    WriteEscapedPropertyName(node, path, ref written);
                }
                else if (node.ArrayIndex >= 0)
                {
                    if (!Utf8Formatter.TryFormat(node.ArrayIndex, formattedArrayIndex, out int formattedLength))
                    {
                        throw new PayloadProtectionFormatException();
                    }

                    WritePathBytes(path, ref written, formattedArrayIndex[..formattedLength]);
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

    private void WriteEscapedPropertyName(BoundedJsonNode node, byte[] path, ref int written)
    {
        int maximumLength = Math.Max(1, node.PropertyTokenLength);
        byte[] decoded = ArrayPool<byte>.Shared.Rent(maximumLength);
        try
        {
            var reader = new Utf8JsonReader(_utf8Json.AsSpan(node.PropertyTokenStart, node.PropertyTokenLength));
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new PayloadProtectionFormatException();
            }

            int decodedLength = reader.CopyString(decoded);
            for (int index = 0; index < decodedLength; index++)
            {
                byte value = decoded[index];
                if (value == (byte)'~')
                {
                    WritePathBytes(path, ref written, "~0"u8);
                }
                else if (value == (byte)'/')
                {
                    WritePathBytes(path, ref written, "~1"u8);
                }
                else
                {
                    WritePathByte(path, ref written, value);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decoded);
            ArrayPool<byte>.Shared.Return(decoded);
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

    private static void WritePathBytes(byte[] destination, ref int written, ReadOnlySpan<byte> value)
    {
        if (value.Length > PayloadProtectionLimits.PathBytes - written)
        {
            throw new PayloadProtectionFormatException();
        }

        value.CopyTo(destination.AsSpan(written));
        written += value.Length;
    }

    private static void WritePathByte(byte[] destination, ref int written, byte value)
    {
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
