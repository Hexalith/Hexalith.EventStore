namespace Hexalith.EventStore.Server.Events;
/// <summary>Marks unavailable completed candidate authority without classifying infrastructure as snapshot absence.</summary>
internal sealed class DaprLogicalAnchorRefusalException : InvalidOperationException
{
    /// <summary>Retains a safe candidate refusal reason for full-replay fallback.</summary>
    internal DaprLogicalAnchorRefusalException(string message) : base(message)
    {
    }
}
