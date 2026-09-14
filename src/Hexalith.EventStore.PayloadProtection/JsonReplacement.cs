namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Describes one non-overlapping raw JSON value replacement.
/// </summary>
/// <param name="Start">The replaced value's byte offset.</param>
/// <param name="Length">The replaced value's byte length.</param>
/// <param name="Value">The complete replacement JSON bytes.</param>
internal sealed record JsonReplacement(int Start, int Length, byte[] Value);
