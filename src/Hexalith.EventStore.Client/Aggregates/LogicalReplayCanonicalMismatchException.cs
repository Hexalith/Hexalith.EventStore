namespace Hexalith.EventStore.Client.Aggregates;
/// <summary>Distinguishes proven canonical-byte mismatch from application or infrastructure callback failure.</summary>
internal sealed class LogicalReplayCanonicalMismatchException : InvalidOperationException
{
    /// <summary>Retains the existing safe canonical-proof refusal reason.</summary>
    internal LogicalReplayCanonicalMismatchException(string message) : base(message)
    {
    }
}
