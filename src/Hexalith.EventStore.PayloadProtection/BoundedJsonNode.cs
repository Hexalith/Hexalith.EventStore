using System.Text.Json;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Describes one JSON value by byte range and structural links without retaining decoded member names.
/// </summary>
internal sealed class BoundedJsonNode
{
    /// <summary>
    /// Initializes a new indexed JSON value.
    /// </summary>
    internal BoundedJsonNode(
        JsonValueKind valueKind,
        int start,
        int length,
        int parentIndex,
        int propertyTokenStart,
        int propertyTokenLength,
        ulong propertyNameHash,
        int arrayIndex,
        int depth,
        bool valueIsEscaped)
    {
        ValueKind = valueKind;
        Start = start;
        Length = length;
        ParentIndex = parentIndex;
        PropertyTokenStart = propertyTokenStart;
        PropertyTokenLength = propertyTokenLength;
        PropertyNameHash = propertyNameHash;
        ArrayIndex = arrayIndex;
        Depth = depth;
        ValueIsEscaped = valueIsEscaped;
    }

    /// <summary>Gets the JSON value kind.</summary>
    internal JsonValueKind ValueKind { get; }

    /// <summary>Gets the value's first byte offset.</summary>
    internal int Start { get; }

    /// <summary>Gets or sets the complete value byte length.</summary>
    internal int Length { get; set; }

    /// <summary>Gets the parent node index, or -1 for the root.</summary>
    internal int ParentIndex { get; }

    /// <summary>Gets the raw property-name token offset, or -1 for array elements and the root.</summary>
    internal int PropertyTokenStart { get; }

    /// <summary>Gets the raw property-name token length.</summary>
    internal int PropertyTokenLength { get; }

    /// <summary>Gets the SHA-256-derived hash of the decoded property name.</summary>
    internal ulong PropertyNameHash { get; }

    /// <summary>Gets the containing-array index, or -1 for object members and the root.</summary>
    internal int ArrayIndex { get; }

    /// <summary>Gets the zero-based structural depth of this node.</summary>
    internal int Depth { get; }

    /// <summary>Gets a value indicating whether a JSON string token contains escape sequences.</summary>
    internal bool ValueIsEscaped { get; }

    /// <summary>Gets or sets the first child node index.</summary>
    internal int FirstChildIndex { get; set; } = -1;

    /// <summary>Gets or sets the last child node index.</summary>
    internal int LastChildIndex { get; set; } = -1;

    /// <summary>Gets or sets the next sibling node index.</summary>
    internal int NextSiblingIndex { get; set; } = -1;
}
