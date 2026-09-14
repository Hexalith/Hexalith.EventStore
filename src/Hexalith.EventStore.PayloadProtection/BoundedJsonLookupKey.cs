namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Identifies one object-member hash or array index beneath an indexed JSON parent.
/// </summary>
/// <param name="ParentIndex">The parent node index.</param>
/// <param name="IsArrayIndex">Whether <paramref name="Value"/> is an array index instead of a member-name hash.</param>
/// <param name="Value">The unsigned member-name hash or array index.</param>
internal readonly record struct BoundedJsonLookupKey(
    int ParentIndex,
    bool IsArrayIndex,
    ulong Value);
