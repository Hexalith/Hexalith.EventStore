namespace Hexalith.EventStore.Contracts.Authorization;

/// <summary>
/// Defines stable ProblemDetails extension names used by EventStore authorization responses.
/// </summary>
public static class AuthorizationProblemDetailsExtensions {
    /// <summary>
    /// The RFC 7807/RFC 9457 extension that carries the stable authorization reason code.
    /// </summary>
    public const string ReasonCode = "reasonCode";
}
