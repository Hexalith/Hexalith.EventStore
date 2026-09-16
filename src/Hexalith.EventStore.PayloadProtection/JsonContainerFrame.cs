// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 6-8, 14, and 15.
using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Tracks one open JSON container and byte-oriented duplicate-member detection during parsing.
/// </summary>
internal sealed class JsonContainerFrame(int nodeIndex, JsonValueKind valueKind) : IDisposable
{
    private readonly Dictionary<ulong, List<byte[]>> _memberNames = [];
    private int _pendingPropertyStart = -1;
    private int _pendingPropertyLength;
    private ulong _pendingPropertyHash;
    private bool _pendingPropertyIsEscaped;

    /// <summary>Gets the indexed container node.</summary>
    internal int NodeIndex { get; } = nodeIndex;

    /// <summary>Gets the container kind.</summary>
    internal JsonValueKind ValueKind { get; } = valueKind;

    /// <summary>Gets or sets the next array element index.</summary>
    internal int NextArrayIndex { get; set; }

    /// <summary>
    /// Records a property name after exact decoded-byte duplicate detection.
    /// </summary>
    internal void SetProperty(Utf8JsonReader reader, int tokenStart, int tokenLength)
    {
        if (ValueKind != JsonValueKind.Object || _pendingPropertyStart >= 0)
        {
            throw new PayloadProtectionFormatException();
        }

        int maximumLength = checked((int)reader.ValueSpan.Length);
        byte[] rented = ArrayPool<byte>.Shared.Rent(Math.Max(1, maximumLength));
        try
        {
            int length = reader.CopyString(rented);
            Span<byte> digest = stackalloc byte[32];
            SHA256.HashData(rented.AsSpan(0, length), digest);
            ulong hash = BinaryPrimitives.ReadUInt64BigEndian(digest);
            if (!_memberNames.TryGetValue(hash, out List<byte[]>? collisions))
            {
                collisions = [];
                _memberNames.Add(hash, collisions);
            }

            for (int index = 0; index < collisions.Count; index++)
            {
                if (rented.AsSpan(0, length).SequenceEqual(collisions[index]))
                {
                    throw new PayloadProtectionFormatException();
                }
            }

            _ = collisions.EnsureCapacity(checked(collisions.Count + 1));
            byte[]? retainedName = rented.AsSpan(0, length).ToArray();
            try
            {
                collisions.Add(retainedName);
                retainedName = null;
            }
            finally
            {
                if (retainedName is not null)
                {
                    CryptographicOperations.ZeroMemory(retainedName);
                }
            }

            _pendingPropertyStart = tokenStart;
            _pendingPropertyLength = tokenLength;
            _pendingPropertyHash = hash;
            _pendingPropertyIsEscaped = reader.ValueIsEscaped;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rented);
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Consumes the pending property token for the next object value.
    /// </summary>
    internal void ConsumeProperty(
        out int tokenStart,
        out int tokenLength,
        out ulong propertyNameHash,
        out bool propertyNameIsEscaped)
    {
        if (ValueKind != JsonValueKind.Object || _pendingPropertyStart < 0)
        {
            throw new PayloadProtectionFormatException();
        }

        tokenStart = _pendingPropertyStart;
        tokenLength = _pendingPropertyLength;
        propertyNameHash = _pendingPropertyHash;
        propertyNameIsEscaped = _pendingPropertyIsEscaped;
        _pendingPropertyStart = -1;
        _pendingPropertyLength = 0;
        _pendingPropertyHash = 0;
        _pendingPropertyIsEscaped = false;
    }

    /// <summary>
    /// Verifies that an object does not end with a property missing its value.
    /// </summary>
    internal void ValidateComplete()
    {
        if (_pendingPropertyStart >= 0)
        {
            throw new PayloadProtectionFormatException();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (KeyValuePair<ulong, List<byte[]>> entry in _memberNames)
        {
            List<byte[]> collisions = entry.Value;
            for (int index = 0; index < collisions.Count; index++)
            {
                CryptographicOperations.ZeroMemory(collisions[index]);
            }
        }

        _memberNames.Clear();
    }
}
