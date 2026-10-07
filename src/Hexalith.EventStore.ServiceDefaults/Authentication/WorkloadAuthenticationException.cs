namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Carries one bounded denial reason through the authentication pipeline.
/// </summary>
/// <remarks>The message is the reason code itself, so no token, claim, or configured value can leak.</remarks>
public sealed class WorkloadAuthenticationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="WorkloadAuthenticationException"/> class.</summary>
    public WorkloadAuthenticationException()
        : this(WorkloadAuthenticationReasons.AssertionInvalid)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WorkloadAuthenticationException"/> class.</summary>
    /// <param name="reasonCode">The bounded reason code.</param>
    public WorkloadAuthenticationException(string reasonCode)
        : base(reasonCode) => ReasonCode = reasonCode;

    /// <summary>Initializes a new instance of the <see cref="WorkloadAuthenticationException"/> class.</summary>
    /// <param name="reasonCode">The bounded reason code.</param>
    /// <param name="innerException">The original validation failure, kept only for in-process diagnostics.</param>
    public WorkloadAuthenticationException(string reasonCode, Exception? innerException)
        : base(reasonCode, innerException) => ReasonCode = reasonCode;

    /// <summary>Gets the bounded reason code.</summary>
    public string ReasonCode { get; }
}
