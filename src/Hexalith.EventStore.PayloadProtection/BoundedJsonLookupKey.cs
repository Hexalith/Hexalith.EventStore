// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 7, 8, and 14.
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
