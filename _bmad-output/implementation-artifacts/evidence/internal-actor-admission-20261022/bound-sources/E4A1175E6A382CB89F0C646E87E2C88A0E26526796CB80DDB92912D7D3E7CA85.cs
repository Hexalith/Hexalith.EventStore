namespace Hexalith.EventStore.Client.Streams;

/// <summary>Retryable refusal when bounded authoritative-read workers remain occupied by providers, including abandoned work.</summary>
public sealed class AuthoritativeStreamReadAdmissionException : InvalidOperationException
{
    /// <summary>Initializes a bounded admission refusal without disclosing provider or source details.</summary>
    public AuthoritativeStreamReadAdmissionException() : base("Authoritative stream read admission is exhausted.")
    {
    }
}
