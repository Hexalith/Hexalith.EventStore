// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 12.1 and 13.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Represents one mixed-history decision: every event readable in order, or only the first unreadable sequence.
/// </summary>
/// <param name="Events">
/// Every routed event in sequence order when the whole stream is readable; otherwise <see langword="null"/>, so no
/// partial list is ever returned.
/// </param>
/// <param name="FirstUnreadable">The first unreadable event decision, or <see langword="null"/>.</param>
internal sealed record CompatibilityStreamReadResult(
    IReadOnlyList<CompatibilityEventReadResult>? Events,
    CompatibilityEventReadResult? FirstUnreadable)
{
    /// <summary>Gets a value indicating whether every event in the stream is readable.</summary>
    internal bool IsReadable => FirstUnreadable is null && Events is not null;

    /// <summary>Creates a completely readable stream result.</summary>
    internal static CompatibilityStreamReadResult Readable(IReadOnlyList<CompatibilityEventReadResult> events)
        => new(events, null);

    /// <summary>Creates the single first-unreadable stream decision.</summary>
    internal static CompatibilityStreamReadResult Unreadable(CompatibilityEventReadResult firstUnreadable)
        => new(null, firstUnreadable);

    /// <inheritdoc/>
    public override string ToString() => nameof(CompatibilityStreamReadResult);
}
