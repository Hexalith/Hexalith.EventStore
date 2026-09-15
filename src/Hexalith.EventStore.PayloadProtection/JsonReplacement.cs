// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 6-8, 14, and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Describes one non-overlapping raw JSON value replacement.
/// </summary>
/// <param name="Start">The replaced value's byte offset.</param>
/// <param name="Length">The replaced value's byte length.</param>
/// <param name="Value">The complete replacement JSON bytes.</param>
internal sealed record JsonReplacement(int Start, int Length, byte[] Value)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(JsonReplacement);
}
