namespace Hexalith.EventStore.Client.Processes;

/// <summary>Signals a transient failure acquiring authoritative read-only process context.</summary>
/// <remarks>Never use this exception for malformed, foreign, unsupported, or undecodable evidence.</remarks>
public sealed class ProcessEvidenceUnavailableException : Exception
{
    /// <summary>Initializes a transient context-read failure without exposing source payloads.</summary>
    public ProcessEvidenceUnavailableException()
        : base("Authoritative process evidence is temporarily unavailable.")
    {
    }

    /// <summary>Initializes a transient context-read failure with its underlying dependency failure.</summary>
    /// <param name="innerException">The read failure that prevented context acquisition.</param>
    public ProcessEvidenceUnavailableException(Exception innerException)
        : base("Authoritative process evidence is temporarily unavailable.", innerException)
    {
    }
}
