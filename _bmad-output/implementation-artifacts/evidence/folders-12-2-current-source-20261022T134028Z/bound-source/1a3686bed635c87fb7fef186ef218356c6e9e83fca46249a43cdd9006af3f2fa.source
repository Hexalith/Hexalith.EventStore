namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Signals that a registered <see cref="IDomainServiceAdministratorVerifier"/> could neither confirm nor refute a wire
/// administrator assertion, so the request must fail closed before any domain work.
/// </summary>
/// <remarks>
/// The message is the bounded reason code only; the original failure is kept as the inner exception for in-process
/// diagnostics and never reaches the response, the log message, or a trace tag.
/// </remarks>
internal sealed class DomainServiceAdministratorVerificationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DomainServiceAdministratorVerificationException"/> class.</summary>
    public DomainServiceAdministratorVerificationException()
        : base(ServiceDefaults.Authentication.WorkloadAuthenticationReasons.AdministratorVerifierUnavailable)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DomainServiceAdministratorVerificationException"/> class.</summary>
    /// <param name="message">The bounded reason.</param>
    public DomainServiceAdministratorVerificationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DomainServiceAdministratorVerificationException"/> class.</summary>
    /// <param name="message">The bounded reason.</param>
    /// <param name="innerException">The verifier failure.</param>
    public DomainServiceAdministratorVerificationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
